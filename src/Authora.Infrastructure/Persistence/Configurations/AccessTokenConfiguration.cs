// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Access Token.
/// </summary>
public sealed class AccessTokenConfiguration : IEntityTypeConfiguration<AccessToken>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AccessToken> builder)
    {
        builder.ToTable("access_tokens", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(Schemas.ScopeMaxLength);

        builder.Ignore(x => x.Scopes);

        builder.HasOne(x => x.Grant)
            .WithMany()
            .HasForeignKey(x => x.GrantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
