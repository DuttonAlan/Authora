// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the challenges of WebAuthn ceremonies.
/// </summary>
public sealed class PasskeyChallengeConfiguration : IEntityTypeConfiguration<PasskeyChallenge>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PasskeyChallenge> builder)
    {
        builder.ToTable("passkey_challenges", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.ChallengeHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength);

        builder.HasIndex(x => x.ChallengeHash)
            .IsUnique();

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.Property(x => x.Ceremony)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
