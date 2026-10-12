// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Authorization Code.
/// </summary>
public sealed class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuthorizationCode> builder)
    {
        builder.ToTable("authorization_codes", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CodeHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength);

        builder.HasIndex(x => x.CodeHash)
            .IsUnique();

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.Property(x => x.RedirectUri)
            .HasMaxLength(2048);

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(Schemas.ScopeMaxLength);

        builder.Property(x => x.CodeChallenge)
            .IsRequired()
            .HasMaxLength(Pkce.CodeChallengeLength);

        builder.Ignore(x => x.Scopes);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
