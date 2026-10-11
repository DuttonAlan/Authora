// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Scope.
/// </summary>
public sealed class ScopeConfiguration : IEntityTypeConfiguration<Scope>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Scope> builder)
    {
        builder.ToTable("scopes", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Scope.NameMaxLength);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(Scope.DisplayNameMaxLength);

        builder.Property(x => x.Description)
            .HasMaxLength(Scope.DescriptionMaxLength);

        builder.Property(x => x.IsSystem)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
