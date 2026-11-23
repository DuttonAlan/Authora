// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Session.
/// </summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    /// <summary>
    ///     Name of the table in the <c>auth</c> schema.
    /// </summary>
    internal const string TableName = "sessions";

    private const string Table = Schemas.Auth + "." + TableName;

    /// <summary>
    ///     Revokes the Session <c>{0}</c> at <c>{1}</c> unless it is already revoked, and returns its Id, see
    ///     <c>SessionRepository.RevokeSession</c>. PostgreSQL checks the condition again after waiting for a concurrent
    ///     update of the row, so of several concurrent revocations exactly one returns the Session.
    /// </summary>
    internal const string RevokeSql =
        "UPDATE " + Table + " SET \"" + nameof(Session.RevokedAt) + "\" = {1}"
        + " WHERE \"" + nameof(Session.Id) + "\" = {0} AND \"" + nameof(Session.RevokedAt) + "\" IS NULL"
        + " RETURNING \"" + nameof(Session.Id) + "\" AS \"Value\"";

    /// <summary>
    ///     Revokes the Sessions of the User <c>{0}</c> at <c>{1}</c> that are not revoked yet, except the Session
    ///     <c>{2}</c> (<see cref="Guid.Empty"/> for none), and returns their Ids, like <see cref="RevokeSql"/>.
    /// </summary>
    internal const string RevokeOfUserSql =
        "UPDATE " + Table + " SET \"" + nameof(Session.RevokedAt) + "\" = {1}"
        + " WHERE \"" + nameof(Session.UserId) + "\" = {0} AND \"" + nameof(Session.RevokedAt) + "\" IS NULL"
        + " AND \"" + nameof(Session.Id) + "\" <> {2}"
        + " RETURNING \"" + nameof(Session.Id) + "\" AS \"Value\"";

    /// <summary>
    ///     Locks the Session <c>{0}</c> until the end of the transaction against updates, e.g. a revocation, see
    ///     <c>SessionRepository.LockSession</c>.
    /// </summary>
    internal const string LockSql =
        "SELECT 1 FROM " + Table + " WHERE \"" + nameof(Session.Id) + "\" = {0} FOR SHARE";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable(TableName, Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength)

            // A step-up gives the Session a new token. Saving a Session whose token changed since it was loaded fails,
            // so two concurrent step-ups cannot overwrite each other, and no request saves a Session it knows only by
            // an outdated token.
            .IsConcurrencyToken();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.Sid)
            .IsRequired()
            .HasMaxLength(Session.SidMaxLength);

        builder.Property(x => x.AuthenticationMethod)
            .IsRequired()
            .HasMaxLength(AuthenticationMethods.MaxLength);

        builder.Ignore(x => x.AuthenticationMethods);

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
