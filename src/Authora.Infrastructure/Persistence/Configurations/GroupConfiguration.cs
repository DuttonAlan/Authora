// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Group.
/// </summary>
public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Group.NameMaxLength);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(Group.DisplayNameMaxLength);

        builder.Property(x => x.Description)
            .HasMaxLength(Group.DescriptionMaxLength);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasMany(x => x.Roles)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "GroupRole",
                right => right.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Group>().WithMany().HasForeignKey("GroupId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("group_roles", Schemas.Identity);
                    join.HasKey("GroupId", "RoleId");
                });

        builder.Navigation(x => x.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
