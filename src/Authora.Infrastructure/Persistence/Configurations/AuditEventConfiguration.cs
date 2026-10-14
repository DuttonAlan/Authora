// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the AuditEvent.
/// </summary>
/// <remarks>
///     The actor and the target have no foreign keys: events must outlive the Users, Clients and objects they name.
/// </remarks>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("events", Schemas.Audit);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(AuditEvent.TypeMaxLength);

        builder.Property(x => x.Outcome)
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasMaxLength(AuditEvent.IpAddressMaxLength);

        builder.Property(x => x.TargetType)
            .HasMaxLength(AuditEvent.TargetTypeMaxLength);

        builder.Property(x => x.TargetId)
            .HasMaxLength(AuditEvent.TargetIdMaxLength);

        builder.Property(x => x.Details)
            .HasMaxLength(AuditEvent.DetailsMaxLength);

        // The search sorts by time, and the cleanup deletes by time.
        builder.HasIndex(x => x.OccurredAt);

        builder.HasIndex(x => new { x.Type, x.OccurredAt });

        builder.HasIndex(x => x.ActorUserId);

        builder.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}
