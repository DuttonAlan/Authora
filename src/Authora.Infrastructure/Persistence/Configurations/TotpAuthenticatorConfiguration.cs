// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of authenticator apps.
/// </summary>
public sealed class TotpAuthenticatorConfiguration : IEntityTypeConfiguration<TotpAuthenticator>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TotpAuthenticator> builder)
    {
        builder.ToTable("totp_authenticators", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // At most one per User, confirmed or not.
        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.ProtectedSecret)
            .IsRequired()
            .HasMaxLength(TotpAuthenticator.ProtectedSecretMaxLength);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Saving an authenticator whose confirmation changed since it was loaded fails, so two concurrent
        // confirmations cannot both issue recovery codes, and a reset cannot miss a confirmation. The uses of codes
        // (LastUsedTimeStep) are atomic updates of their own and do not conflict with it.
        builder.Property(x => x.ConfirmedAt)
            .IsConcurrencyToken();

        builder.Ignore(x => x.IsConfirmed);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
