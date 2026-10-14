// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Roles.</param>
public sealed class RoleRepository(AuthoraContext context) : IRoleRepository
{
    /// <inheritdoc/>
    public async Task<Guid> CreateRole(Role role, CancellationToken cancellationToken = default)
    {
        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return role.Id;
    }

    /// <inheritdoc/>
    public async Task<Role?> GetRoleById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .Include(role => role.Permissions)
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Role>> GetRoles(CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> NameExists(string name, CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .AnyAsync(role => role.Name == name, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountUsersWithRole(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .CountAsync(user => user.Roles.Any(role => role.Id == roleId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteRole(Role role, CancellationToken cancellationToken = default)
    {
        // user_roles, group_roles and role_permissions are removed by the database (cascade delete).
        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
