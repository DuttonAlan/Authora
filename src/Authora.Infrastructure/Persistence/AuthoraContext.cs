// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     The DbContext to interact with the Database.
/// </summary>
public sealed class AuthoraContext(DbContextOptions<AuthoraContext> options) : DbContext(options)
{
    /// <summary>
    ///     The Users Table.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    ///     The Clients Table. Client Secrets and Redirect URIs are loaded and saved together with their Client.
    /// </summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>
    ///     The Roles Table.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    ///     The Groups Table.
    /// </summary>
    public DbSet<Group> Groups => Set<Group>();

    /// <summary>
    ///     The Permissions Table.
    /// </summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    ///     The Scopes Table.
    /// </summary>
    public DbSet<Scope> Scopes => Set<Scope>();

    /// <summary>
    ///     The authenticator apps of Users, with their encrypted secrets.
    /// </summary>
    public DbSet<TotpAuthenticator> TotpAuthenticators => Set<TotpAuthenticator>();

    /// <summary>
    ///     The hashes of the recovery codes of Users.
    /// </summary>
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();

    /// <summary>
    ///     The Sessions Table in the <c>auth</c> schema.
    /// </summary>
    public DbSet<Session> Sessions => Set<Session>();

    /// <summary>
    ///     The pending sign-ins that wait for a second factor, in the <c>auth</c> schema.
    /// </summary>
    public DbSet<SignInChallenge> SignInChallenges => Set<SignInChallenge>();

    /// <summary>
    ///     The Authorization Codes Table in the <c>auth</c> schema.
    /// </summary>
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();

    /// <summary>
    ///     The Grants Table in the <c>auth</c> schema.
    /// </summary>
    public DbSet<Grant> Grants => Set<Grant>();

    /// <summary>
    ///     The Access Tokens Table in the <c>auth</c> schema.
    /// </summary>
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    /// <summary>
    ///     The Refresh Tokens Table in the <c>auth</c> schema.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    ///     The Consents of Users for Clients in the <c>auth</c> schema. Their Scopes are loaded and saved with them.
    /// </summary>
    public DbSet<Consent> Consents => Set<Consent>();

    /// <summary>
    ///     The one-time tokens of email links in the <c>auth</c> schema.
    /// </summary>
    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();

    /// <summary>
    ///     The Audit Events Table in the <c>audit</c> schema.
    /// </summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>
    ///     The hash chain of the audit events in the <c>audit</c> schema.
    /// </summary>
    public DbSet<AuditChainLink> AuditChainLinks => Set<AuditChainLink>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthoraContext).Assembly);
    }
}
