// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the Client Secret.
///     The relationship to the Client is configured in <see cref="ClientConfiguration"/>.
/// </summary>
public sealed class ClientSecretConfiguration : IEntityTypeConfiguration<ClientSecret>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ClientSecret> builder)
    {
        builder.ToTable("client_secrets", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SecretHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
