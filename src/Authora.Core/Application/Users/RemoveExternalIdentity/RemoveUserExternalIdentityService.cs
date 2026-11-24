// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.ExternalSignIn;
using Authora.Core.Application.ExternalSignIn.Unlink;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.RemoveExternalIdentity;

/// <summary>
///     The Service for an administrator to remove the link between a User and their account at an external identity
///     provider.
/// </summary>
/// <remarks>
///     Removing a link only prevents future sign-ins through it; the Sessions of the User stay (revoke them separately).
///     A User without a password keeps their last link, which is then their only way to sign in: set a password or
///     send a reset link first. The caller must be allowed to act on the User (<see cref="PrivilegeGuard"/>).
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository for the lock of the User's ways to sign in.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to remove the link.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="auditLog">The audit log to record the removal.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveUserExternalIdentityService(
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    IExternalIdentityRepository externalIdentityRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<RemoveUserExternalIdentityService> logger)
{
    /// <summary>
    ///     Removes a link of a User.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="externalIdentityId">Id of the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the removal is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the User or the link does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted, or the link is their last way to sign in.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(Guid userId, Guid externalIdentityId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var entry = AuditEntry.ForUser(
            AuditEventTypes.ExternalIdentities.Unlinked,
            user.Id,
            $"Identity {externalIdentityId}, by an administrator");

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        // Joins the transaction of the guard, so a removal by the User or a link cannot run at the same time.
        await using var userTransaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        var identities = await externalIdentityRepository.GetIdentitiesOfUser(user.Id, cancellationToken);
        var identity = identities.FirstOrDefault(identity => identity.Id == externalIdentityId)
            ?? throw new NotFoundException($"Linked account '{externalIdentityId}' was not found.");

        if (UnlinkExternalIdentityService.IsLastSignInMethod(user.HasPassword, identities.Count))
        {
            throw new ConflictException("The User has no password and no other linked account, so this is their only way to sign in. Set a password first.");
        }

        externalIdentityRepository.RemoveIdentities([identity]);

        auditLog.Record(entry with
        {
            Details = $"Identity {identity.Id}, identity provider {identity.ProviderId}, by an administrator"
        });

        await externalIdentityRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.ExternalIdentityRemovedByAdministrator(user.Id, identity.Id, identity.ProviderId);
    }
}
