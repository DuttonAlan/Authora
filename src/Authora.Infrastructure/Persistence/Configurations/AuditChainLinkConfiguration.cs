// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the AuditChainLink.
/// </summary>
/// <remarks>
///     No foreign key to the event: a cascade would silently remove the link of a deleted event, and a restriction
///     would hide the deletion behind an error. A link without its event is what the verification detects.
/// </remarks>
public sealed class AuditChainLinkConfiguration : IEntityTypeConfiguration<AuditChainLink>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditChainLink> builder)
    {
        builder.ToTable("chain", Schemas.Audit);

        builder.HasKey(x => x.Sequence);

        // Assigned by the sealing, which continues the last Sequence under a lock.
        builder.Property(x => x.Sequence)
            .ValueGeneratedNever();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.PreviousHash)
            .IsRequired()
            .HasMaxLength(AuditChainLink.HashLength)
            .IsFixedLength();

        builder.Property(x => x.Hash)
            .IsRequired()
            .HasMaxLength(AuditChainLink.HashLength)
            .IsFixedLength();

        builder.Property(x => x.SealedAt)
            .IsRequired();

        // An event is sealed once. The sealing looks up which events are sealed already.
        builder.HasIndex(x => x.EventId)
            .IsUnique();
    }
}
