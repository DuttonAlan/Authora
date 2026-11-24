// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the accounts at external identity providers linked to Users.
/// </summary>
public sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Subject)
            .IsRequired()
            .HasMaxLength(ExternalIdentity.SubjectMaxLength);

        builder.Property(x => x.Email)
            .HasMaxLength(User.EmailMaxLength);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // An account at a provider signs in as exactly one User.
        builder.HasIndex(x => new { x.ProviderId, x.Subject })
            .IsUnique();

        // A User links at most one account per provider.
        builder.HasIndex(x => new { x.UserId, x.ProviderId })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A provider with linked accounts cannot be deleted, see DeleteIdentityProviderService.
        builder.HasOne<IdentityProvider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
