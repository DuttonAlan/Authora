// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Seeding;

/// <summary>
///     Seeds the data Authora needs at startup: the Permissions of <see cref="PermissionNames"/>, the administrator
///     Role with all Permissions, the <see cref="AdminApiScope"/>, and the first administrator (US-003).
/// </summary>
/// <remarks>
///     Idempotent: only what is missing is created, so it runs on every start. It runs at runtime instead of
///     <c>HasData</c>, because Ids are version 7 Guids created in code.
/// </remarks>
/// <param name="context">The DbContext to seed.</param>
/// <param name="passwordHasher">The hasher for the password of the first administrator.</param>
/// <param name="options">The settings of the first administrator.</param>
/// <param name="logger">The Logger to record what was created.</param>
public sealed class IdentitySeeder(
    AuthoraContext context,
    IPasswordHasher passwordHasher,
    IOptions<BootstrapOptions> options,
    ILogger<IdentitySeeder> logger)
{
    /// <summary>
    ///     Seeds the missing data.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the seeding.</param>
    /// <returns>A Task that completes once the data is seeded.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a regular Role or Scope uses the Name of the administrator Role or the admin API Scope, or the
    ///     first administrator has to be created but has no password or an Email that is already used by another User.
    /// </exception>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Several instances starting at the same time would otherwise all try to create the same rows.
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.Seed})", cancellationToken);

        var permissions = await SeedPermissionsAsync(cancellationToken);
        var administratorRole = await SeedAdministratorRoleAsync(permissions, cancellationToken);
        var adminApiScope = await SeedAdminApiScopeAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await BootstrapAdministratorAsync(administratorRole, cancellationToken);
        await BootstrapAdminClientAsync(adminApiScope, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<List<Permission>> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions.ToListAsync(cancellationToken);
        var existingNames = permissions.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);

        var missing = PermissionNames.All
            .Where(name => !existingNames.Contains(name))
            .Select(name => new Permission { Name = name })
            .ToList();

        if (missing.Count > 0)
        {
            context.Permissions.AddRange(missing);
            permissions.AddRange(missing);

            logger.PermissionsSeeded(missing.Count);
        }

        return permissions;
    }

    private async Task<Role> SeedAdministratorRoleAsync(List<Permission> permissions, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(role => role.Permissions)
            .FirstOrDefaultAsync(role => role.Name == Role.AdministratorName, cancellationToken);

        if (role is null)
        {
            role = Role.CreateSystem(Role.AdministratorName, "Administrator", "Has all Permissions.");
            context.Roles.Add(role);

            logger.AdministratorRoleCreated(role.Id);
        }
        else if (!role.IsSystem)
        {
            // Never promote a Role that someone created through the API to the administrator Role.
            throw new InvalidOperationException(
                $"The regular Role '{role.Id}' uses the Name '{Role.AdministratorName}' of the administrator Role. Rename it.");
        }

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission);
        }

        return role;
    }

    private async Task<Scope> SeedAdminApiScopeAsync(CancellationToken cancellationToken)
    {
        var scope = await context.Scopes
            .FirstOrDefaultAsync(scope => scope.Name == AdminApiScope.Name, cancellationToken);

        if (scope is null)
        {
            scope = Scope.CreateSystem(AdminApiScope.Name, AdminApiScope.DisplayName, AdminApiScope.Description);
            context.Scopes.Add(scope);

            logger.SystemScopeCreated(scope.Id, scope.Name);
        }
        else if (!scope.IsSystem)
        {
            // A regular Scope can be deleted through the API, which would remove the admin API Scope from every Client.
            // Never promote it either: the Clients someone allowed it would silently gain admin API access for good.
            throw new InvalidOperationException(
                $"The regular Scope '{scope.Id}' uses the Name '{AdminApiScope.Name}' of the admin API Scope. Rename it.");
        }

        return scope;
    }

    private async Task BootstrapAdministratorAsync(Role administratorRole, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var administratorExists = await context.Users
            .Where(user => user.State == UserState.Active || user.State == UserState.Inactive)
            .AnyAsync(user => user.Roles.Any(role => role.Id == administratorRole.Id), cancellationToken);

        if (administratorExists)
        {
            if (!string.IsNullOrEmpty(settings.Administrator.Password))
            {
                logger.BootstrapPasswordNotRemoved();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Administrator.Email))
        {
            logger.NoAdministrator();
            return;
        }

        if (string.IsNullOrEmpty(settings.Administrator.Password))
        {
            throw new InvalidOperationException(
                $"'{BootstrapOptions.SectionName}:{nameof(BootstrapOptions.Administrator)}:"
                + $"{nameof(BootstrapAdministratorOptions.Password)}' must be set to create the first administrator.");
        }

        var normalizedEmail = User.Normalize(settings.Administrator.Email);

        // Taking over an existing account would hand it administrator access without its owner noticing.
        if (await context.Users.AnyAsync(existing => existing.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The Email in '{BootstrapOptions.SectionName}:{nameof(BootstrapOptions.Administrator)}' is already used by "
                + "another User. Use a new Email for the first administrator.");
        }

        var user = User.Create(
            settings.Administrator.Email,
            settings.Administrator.FirstName,
            settings.Administrator.LastName,
            passwordHasher.Hash(settings.Administrator.Password));

        user.AssignRole(administratorRole);
        context.Users.Add(user);

        logger.AdministratorBootstrapped(user.Id);
    }

    // Independent of the administrator, so a Client configured after the administrator was created is still created.
    // Registering a Client through the API needs an admin token, which in turn needs a Client.
    private async Task BootstrapAdminClientAsync(Scope adminApiScope, CancellationToken cancellationToken)
    {
        var settings = options.Value.Client;

        if (settings.RedirectUris.Count == 0)
        {
            return;
        }

        var adminClientExists = await context.Clients
            .Where(client => client.Status == ClientStatus.Enabled)
            .AnyAsync(client => client.AllowedScopes.Any(scope => scope.Id == adminApiScope.Id), cancellationToken);

        if (adminClientExists)
        {
            return;
        }

        var client = Client.Create(settings.Name, ClientType.Public);

        foreach (var redirectUri in settings.RedirectUris)
        {
            client.AddRedirectUri(redirectUri);
        }

        client.AllowScope(adminApiScope);
        context.Clients.Add(client);

        logger.AdminClientBootstrapped(client.Id, client.Name);
    }
}
