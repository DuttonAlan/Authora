// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     The DbContext to interact with the Database.
/// </summary>
public sealed class AuthoraContext(DbContextOptions<AuthoraContext> options) : DbContext(options)
{
    /// <summary>
    ///     The Users Table.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthoraContext).Assembly);
    }
}
