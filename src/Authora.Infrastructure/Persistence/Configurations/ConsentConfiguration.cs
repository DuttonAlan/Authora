// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Consents of Users, including their Scopes.
/// </summary>
public sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Consent> builder)
    {
        builder.ToTable("consents", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // At most one Consent per User and Client; also used to look it up.
        builder.HasIndex(x => new { x.UserId, x.ClientId })
            .IsUnique();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a Scope removes it from every Consent, so a new Scope with the same Name is asked for again.
        builder.HasMany(x => x.Scopes)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ConsentScope",
                right => right.HasOne<Scope>().WithMany().HasForeignKey("ScopeId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Consent>().WithMany().HasForeignKey("ConsentId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("consent_scopes", Schemas.Auth);
                    join.HasKey("ConsentId", "ScopeId");
                });

        builder.Navigation(x => x.Scopes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
