// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Session.
/// </summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TokenHash)
            .HasConversion(hash => hash.Value, value => SecretHash.FromHashedValue(value))
            .IsRequired()
            .HasMaxLength(Schemas.HashMaxLength)

            // A step-up gives the Session a new token. Saving a Session whose token changed since it was loaded fails,
            // so two concurrent step-ups cannot overwrite each other, and no request saves a Session it knows only by
            // an outdated token.
            .IsConcurrencyToken();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.Property(x => x.AuthenticationMethod)
            .IsRequired()
            .HasMaxLength(AuthenticationMethods.MaxLength);

        builder.Ignore(x => x.AuthenticationMethods);

        // Used by the cleanup of expired rows, see ExpiredDataCleaner.
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
