// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Authorization;

/// <summary>
///     Enforces that nobody grants more than they hold: a caller who is not an administrator can only give out
///     Permissions they hold themselves, and can only act on Users whose Permissions are a subset of their own.
///     Only an administrator can assign the administrator Role or act on an administrator.
/// </summary>
/// <remarks>
///     The caller is the User of the current request (<see cref="IAuditActorAccessor"/>); without one, every check
///     fails. Call <see cref="BeginChangeAsync"/> before loading what is checked and commit the transaction after
///     saving the change: every checked change holds the same lock, so e.g. the target cannot gain the administrator
///     Role between the check and setting its password. A denied attempt is recorded in the audit log with
///     <see cref="AuditOutcome.Failed"/> and throws a <see cref="ForbiddenException"/>.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the caller and the target User.</param>
/// <param name="roleRepository">An Instance of a IRoleRepository to load the Roles of a Group.</param>
/// <param name="actorAccessor">Provides the caller of the current request.</param>
/// <param name="auditLog">The audit log to record denied attempts.</param>
/// <param name="logger">The Logger to record denied attempts.</param>
public sealed class PrivilegeGuard(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IAuditActorAccessor actorAccessor,
    IAuditLog auditLog,
    ILogger<PrivilegeGuard> logger)
{
    /// <summary>
    ///     Starts the transaction that holds the privilege lock, see <see cref="IUserRepository.BeginPrivilegeChangeAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction, which holds the lock.</returns>
    public Task<ITransaction> BeginChangeAsync(CancellationToken cancellationToken = default)
        => userRepository.BeginPrivilegeChangeAsync(cancellationToken);

    /// <summary>
    ///     Ensures that the caller may assign a Role to a User or a Group: the caller is an administrator, or the Role
    ///     is not the administrator Role and the caller holds all of its Permissions.
    /// </summary>
    /// <param name="role">The Role, with its Permissions loaded.</param>
    /// <param name="attempt">The audit event of the change, recorded as failed if it is denied.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the check passed.</returns>
    /// <exception cref="ForbiddenException">Thrown when the caller may not assign the Role.</exception>
    public async Task EnsureCanAssignRoleAsync(Role role, AuditEntry attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await EnsureCanGrantRolesAsync([role], attempt, cancellationToken);
    }

    /// <summary>
    ///     Ensures that the caller may add a User to a Group: the caller is an administrator or holds all
    ///     Permissions of all Roles of the Group.
    /// </summary>
    /// <param name="group">The Group, with its Roles loaded.</param>
    /// <param name="attempt">The audit event of the change, recorded as failed if it is denied.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the check passed.</returns>
    /// <exception cref="ForbiddenException">Thrown when the caller may not add Users to the Group.</exception>
    public async Task EnsureCanGrantRolesOfGroupAsync(Group group, AuditEntry attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.Roles.Count == 0)
        {
            return;
        }

        var roles = await roleRepository.GetRolesWithPermissionsByIds(
            group.Roles.Select(role => role.Id).ToList(),
            cancellationToken);

        await EnsureCanGrantRolesAsync(roles, attempt, cancellationToken);
    }

    /// <summary>
    ///     Ensures that the caller may grant a Permission to a Role: the caller is an administrator or holds the
    ///     Permission.
    /// </summary>
    /// <param name="permission">The Permission.</param>
    /// <param name="attempt">The audit event of the change, recorded as failed if it is denied.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the check passed.</returns>
    /// <exception cref="ForbiddenException">Thrown when the caller does not hold the Permission.</exception>
    public async Task EnsureCanGrantPermissionAsync(
        Permission permission,
        AuditEntry attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var caller = await GetCallerAsync(cancellationToken);

        if (!caller.IsAdministrator && !caller.Permissions.Contains(permission.Name))
        {
            await DenyAsync(caller, attempt, $"The caller does not hold the Permission '{permission.Name}'.", cancellationToken);
        }
    }

    /// <summary>
    ///     Ensures that the caller may act on a User, e.g. set its password or deactivate it: the caller is an
    ///     administrator, or the User is no administrator and all its effective Permissions are held by the caller.
    ///     Does nothing if the User does not exist.
    /// </summary>
    /// <param name="userId">Id of the target User.</param>
    /// <param name="attempt">The audit event of the change, recorded as failed if it is denied.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the check passed.</returns>
    /// <exception cref="ForbiddenException">Thrown when the User has rights that the caller does not hold.</exception>
    public async Task EnsureCanActOnUserAsync(Guid userId, AuditEntry attempt, CancellationToken cancellationToken = default)
    {
        var caller = await GetCallerAsync(cancellationToken);

        if (caller.IsAdministrator)
        {
            return;
        }

        var target = await userRepository.GetUserWithPermissionsById(userId, cancellationToken);

        if (target is null)
        {
            return;
        }

        var targetRoles = target.GetEffectiveRoles().ToList();

        if (targetRoles.Any(role => role.IsAdministrator))
        {
            await DenyAsync(caller, attempt, "Only an administrator can act on an administrator.", cancellationToken);
        }

        var missing = MissingPermissions(caller, targetRoles);

        if (missing.Count > 0)
        {
            await DenyAsync(
                caller,
                attempt,
                $"The User has Permissions that the caller does not hold: {FormatNames(missing)}.",
                cancellationToken);
        }
    }

    private async Task EnsureCanGrantRolesAsync(
        IReadOnlyCollection<Role> roles,
        AuditEntry attempt,
        CancellationToken cancellationToken)
    {
        var caller = await GetCallerAsync(cancellationToken);

        if (caller.IsAdministrator)
        {
            return;
        }

        if (roles.Any(role => role.IsAdministrator))
        {
            await DenyAsync(caller, attempt, "Only an administrator can assign the administrator Role.", cancellationToken);
        }

        var missing = MissingPermissions(caller, roles);

        if (missing.Count > 0)
        {
            await DenyAsync(
                caller,
                attempt,
                $"The caller does not hold all Permissions of the Role: {FormatNames(missing)}.",
                cancellationToken);
        }
    }

    private async Task<Caller> GetCallerAsync(CancellationToken cancellationToken)
    {
        // Without a User (e.g. outside of a request), nobody holds any rights, so every check fails.
        if (actorAccessor.Current.UserId is not { } callerId)
        {
            return new Caller(null, IsAdministrator: false, new HashSet<string>(StringComparer.Ordinal));
        }

        var user = await userRepository.GetUserWithPermissionsById(callerId, cancellationToken);

        // Same rule as the PermissionEvaluator: a User who cannot sign in holds no rights.
        if (user is null || !user.CanSignIn)
        {
            return new Caller(callerId, IsAdministrator: false, new HashSet<string>(StringComparer.Ordinal));
        }

        var roles = user.GetEffectiveRoles().ToList();

        return new Caller(
            callerId,
            roles.Any(role => role.IsAdministrator),
            roles.SelectMany(role => role.Permissions).Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal));
    }

    private static List<string> MissingPermissions(Caller caller, IEnumerable<Role> roles)
    {
        return roles
            .SelectMany(role => role.Permissions)
            .Select(permission => permission.Name)
            .Where(name => !caller.Permissions.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string FormatNames(IEnumerable<string> names)
        => string.Join(", ", names.Select(name => $"'{name}'"));

    private async Task DenyAsync(Caller caller, AuditEntry attempt, string reason, CancellationToken cancellationToken)
    {
        await auditLog.RecordNowAsync(
            attempt with
            {
                Outcome = AuditOutcome.Failed,
                Details = attempt.Details is null ? $"Denied: {reason}" : $"{attempt.Details}. Denied: {reason}"
            },
            cancellationToken);

        logger.PrivilegeDenied(caller.Id, attempt.Type, attempt.TargetType, attempt.TargetId, reason);

        throw new ForbiddenException(reason);
    }

    /// <summary>
    ///     The rights of the caller.
    /// </summary>
    /// <param name="Id">Id of the caller, or null if the request has no User.</param>
    /// <param name="IsAdministrator">Whether the caller has the administrator Role.</param>
    /// <param name="Permissions">The Names of the effective Permissions of the caller.</param>
    private sealed record Caller(Guid? Id, bool IsAdministrator, IReadOnlySet<string> Permissions);
}
