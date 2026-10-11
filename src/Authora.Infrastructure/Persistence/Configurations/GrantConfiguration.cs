// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Grant.
/// </summary>
public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("grants", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(Schemas.ScopeMaxLength);

        builder.Ignore(x => x.Scopes);
        builder.Ignore(x => x.IsRevoked);

        // Used to revoke the Grants of a reused Authorization Code and of a disabled Client.
        builder.HasIndex(x => x.AuthorizationCodeId);
        builder.HasIndex(x => x.ClientId);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // AuthorizationCodeId has no foreign key on purpose: used and expired codes can be deleted
        // without deleting the Grants and tokens that were issued from them.
    }
}
