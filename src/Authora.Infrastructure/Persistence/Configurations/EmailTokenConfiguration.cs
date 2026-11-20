// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the one-time tokens of email links.
/// </summary>
public sealed class EmailTokenConfiguration : IEntityTypeConfiguration<EmailToken>
{
    /// <summary>
    ///     Name of the table in the <c>auth</c> schema.
    /// </summary>
    internal const string TableName = "email_tokens";

    /// <summary>
    ///     Deletes up to <c>{1}</c> tokens that expired before <c>{0}</c>, see <c>ExpiredDataCleaner</c>. Uses the
    ///     index on <c>ExpiresAt</c>.
    /// </summary>
    internal const string DeleteExpiredBatchSql =
        "DELETE FROM " + Schemas.Auth + "." + TableName
        + " WHERE \"" + nameof(EmailToken.Id) + "\" IN (SELECT \"" + nameof(EmailToken.Id) + "\" FROM " + Schemas.Auth + "." + TableName
        + " WHERE \"" + nameof(EmailToken.ExpiresAt) + "\" < {0} LIMIT {1})";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailToken> builder)
    {
        builder.ToTable(TableName, Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        // Used when a new token replaces the unused ones of the User, see IEmailTokenRepository.RevokeEmailTokens.
        builder.HasIndex(x => new { x.UserId, x.Purpose });

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.Property(x => x.Purpose)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(User.EmailMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
