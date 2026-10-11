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
/// <param name="context">The DbContext to access the Users.</param>
public sealed class UserRepository(AuthoraContext context) : IUserRepository
{
    // Npgsql disables escaping in LIKE by default, so the escape character must be passed explicitly.
    private const string LikeEscapeCharacter = @"\";

    /// <inheritdoc/>
    public async Task<Guid> CreateUser(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    /// <inheritdoc/>
    public async Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<User?> GetUserWithPermissionsById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .AsSplitQuery()
            .Include(user => user.Roles)
            .ThenInclude(role => role.Permissions)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PagedResponse<User>> GetUsers(
        UserState? state,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking();

        query = state is null
            ? query.Where(user => user.State != UserState.Deleted)
            : query.Where(user => user.State == state.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";

            query = query.Where(user =>
                EF.Functions.ILike(user.Email, pattern, LikeEscapeCharacter)
                || EF.Functions.ILike(user.FirstName, pattern, LikeEscapeCharacter)
                || EF.Functions.ILike(user.LastName, pattern, LikeEscapeCharacter));
        }

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
    public async Task<bool> EmailExists(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountUsersWithRoleWhoCanSignIn(
        Guid roleId,
        Guid excludedUserId,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .Where(user => user.Id != excludedUserId)
            .Where(user => user.State == UserState.Active || user.State == UserState.Inactive)
            .CountAsync(user => user.Roles.Any(role => role.Id == roleId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Escapes the wildcard characters of a LIKE pattern, so the search text is matched literally.
    /// </summary>
    /// <param name="value">The search text.</param>
    /// <returns>The escaped search text.</returns>
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}
