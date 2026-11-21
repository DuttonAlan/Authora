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
///     Configures the database mapping of pending sign-ins.
/// </summary>
public sealed class SignInChallengeConfiguration : IEntityTypeConfiguration<SignInChallenge>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SignInChallenge> builder)
    {
        builder.ToTable("sign_in_challenges", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.Property(x => x.AuthenticationMethod)
            .IsRequired()
            .HasMaxLength(AuthenticationMethods.MaxLength);

        builder.Ignore(x => x.AuthenticationMethods);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
