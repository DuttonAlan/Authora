// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Grant.
/// </summary>
public sealed class GrantConfiguration : IEntityTypeConfiguration<Grant>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Grant> builder)
    {
        builder.ToTable("grants", Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Scope)
            .IsRequired()
            .HasMaxLength(Schemas.ScopeMaxLength);

        builder.Ignore(x => x.Scopes);

        builder.PrimitiveCollection(x => x.Resources)
            .IsRequired();

        builder.Ignore(x => x.IsRevoked);
        builder.Ignore(x => x.HasUser);

        // Optional: Grants of the Client itself, and Grants from before the methods were recorded, have none.
        builder.Property(x => x.AuthenticationMethod)
            .HasMaxLength(AuthenticationMethods.MaxLength);

        builder.Ignore(x => x.AuthenticationMethods);

        // Used to revoke the Grants of a reused Authorization Code and of a disabled Client.
        builder.HasIndex(x => x.AuthorizationCodeId);
        builder.HasIndex(x => x.ClientId);

        // Used to revoke the Grants of an ended Session and to find the Clients to notify.
        builder.HasIndex(x => x.SessionId);

        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional: Grants of the Client Credentials Grant Type have no User.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        // AuthorizationCodeId and SessionId have no foreign key on purpose: used and expired codes and ended Sessions can
        // be deleted without deleting the Grants and tokens that were issued from them.
    }
}
