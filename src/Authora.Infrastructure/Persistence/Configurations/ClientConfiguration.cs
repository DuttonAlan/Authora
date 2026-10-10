// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Client, including its Secrets and Redirect URIs.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AccessTokenTTL)
            .IsRequired();

        builder.Property(x => x.RefreshTokenTTL)
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.AllowedGrantTypes)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasMany(x => x.Secrets)
            .WithOne()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Secrets)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(x => x.RedirectUris, redirectUri =>
        {
            redirectUri.ToTable("client_redirect_uris", "identity");

            redirectUri.WithOwner()
                .HasForeignKey("ClientId");

            redirectUri.Property(x => x.Value)
                .HasColumnName("Uri")
                .IsRequired()
                .HasMaxLength(2048);

            redirectUri.HasKey("ClientId", nameof(RedirectUri.Value));
        });

        builder.Navigation(x => x.RedirectUris)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.AllowedScopes)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ClientScope",
                right => right.HasOne<Scope>().WithMany().HasForeignKey("ScopeId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Client>().WithMany().HasForeignKey("ClientId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("client_scopes", "identity");
                    join.HasKey("ClientId", "ScopeId");
                });

        builder.Navigation(x => x.AllowedScopes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
