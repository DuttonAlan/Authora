// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Data;

/// <summary>
///     The DbContext to interact with the Database.
/// </summary>
public sealed class AuthoraContext(DbContextOptions<AuthoraContext> options) : DbContext(options)
{
    /// <summary>
    ///     The User Table.
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
