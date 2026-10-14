// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Groups.</param>
public sealed class GroupRepository(AuthoraContext context) : IGroupRepository
{
    /// <inheritdoc/>
    public async Task<Guid> CreateGroup(Group group, CancellationToken cancellationToken = default)
    {
        context.Groups.Add(group);
        await context.SaveChangesAsync(cancellationToken);

        return group.Id;
    }

    /// <inheritdoc/>
    public async Task<Group?> GetGroupById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .Include(group => group.Roles)
            .FirstOrDefaultAsync(group => group.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Group>> GetGroups(CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedResponse<User>> GetMembers(
        Guid groupId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users
            .AsNoTracking()
            .Where(user => user.State != UserState.Deleted)
            .Where(user => user.Groups.Any(group => group.Id == groupId));

        var total = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Skip(page.Skip)
            .Take(page.Size)
            .ToListAsync(cancellationToken);

        return new PagedResponse<User>(users, total);
    }

    /// <inheritdoc/>
    public async Task<bool> NameExists(string name, CancellationToken cancellationToken = default)
    {
        return await context.Groups
            .AnyAsync(group => group.Name == name, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountMembers(Guid groupId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .CountAsync(user => user.Groups.Any(group => group.Id == groupId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteGroup(Group group, CancellationToken cancellationToken = default)
    {
        // group_members and group_roles are removed by the database (cascade delete).
        context.Groups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
