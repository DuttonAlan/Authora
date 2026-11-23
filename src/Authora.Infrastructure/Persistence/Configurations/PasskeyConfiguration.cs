// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of passkeys.
/// </summary>
public sealed class PasskeyConfiguration : IEntityTypeConfiguration<Passkey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Passkey> builder)
    {
        builder.ToTable("passkeys", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(Passkey.NameMaxLength);

        // A credential belongs to one passkey of one User, so a response always identifies a single passkey.
        builder.Property(x => x.CredentialId)
            .IsRequired()
            .HasMaxLength(Passkey.CredentialIdMaxLength);

        builder.HasIndex(x => x.CredentialId)
            .IsUnique();

        builder.Property(x => x.PublicKey)
            .IsRequired()
            .HasMaxLength(Passkey.PublicKeyMaxLength);

        // Changed by atomic updates of their own when the passkey is used, see PasskeyRepository.TryRecordUse.
        builder.Property(x => x.SignCount)
            .IsRequired();

        builder.Property(x => x.Transports)
            .IsRequired()
            .HasMaxLength(Passkey.TransportsMaxLength);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Ignore(x => x.AuthenticationMethod);
        builder.Ignore(x => x.TransportList);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
