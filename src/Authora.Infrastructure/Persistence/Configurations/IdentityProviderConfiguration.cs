// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of external identity providers.
/// </summary>
public sealed class IdentityProviderConfiguration : IEntityTypeConfiguration<IdentityProvider>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdentityProvider> builder)
    {
        builder.ToTable("identity_providers", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(IdentityProvider.NameMaxLength);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(IdentityProvider.SlugMaxLength);

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Preset)
            .IsRequired();

        builder.Property(x => x.Issuer)
            .IsRequired()
            .HasMaxLength(IdentityProvider.IssuerMaxLength);

        builder.Property(x => x.ClientId)
            .IsRequired()
            .HasMaxLength(IdentityProvider.ClientIdMaxLength);

        // Encrypted with Data Protection, see IProviderSecretProtector; never the plain-text secret.
        builder.Property(x => x.ProtectedClientSecret)
            .HasMaxLength(IdentityProvider.ProtectedClientSecretMaxLength);

        builder.Property(x => x.Scopes)
            .IsRequired()
            .HasMaxLength(IdentityProvider.ScopesMaxLength);

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.AllowSignUp)
            .IsRequired();

        // A PostgreSQL text[]; replaced as a whole by ConfigureSignUp.
        builder.PrimitiveCollection(x => x.AllowedEmailDomains)
            .HasField("_allowedEmailDomains")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // Deleting a Group removes it from the default Groups of every provider.
        builder.HasMany(x => x.DefaultGroups)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "IdentityProviderDefaultGroup",
                right => right.HasOne<Group>().WithMany().HasForeignKey("GroupId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<IdentityProvider>().WithMany().HasForeignKey("IdentityProviderId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("identity_provider_default_groups", Schemas.Identity);
                    join.HasKey("IdentityProviderId", "GroupId");
                });

        builder.Navigation(x => x.DefaultGroups)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.HasClientSecret);
        builder.Ignore(x => x.ScopeList);
    }
}
