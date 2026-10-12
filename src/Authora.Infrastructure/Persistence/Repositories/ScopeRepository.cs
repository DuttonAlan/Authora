// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Scopes.</param>
public sealed class ScopeRepository(AuthoraContext context) : IScopeRepository
{
    /// <inheritdoc/>
    public async Task<Guid> CreateScope(Scope scope, CancellationToken cancellationToken = default)
    {
        context.Scopes.Add(scope);
        await context.SaveChangesAsync(cancellationToken);

        return scope.Id;
    }

    /// <inheritdoc/>
    public async Task<Scope?> GetScopeById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Scopes
            .FirstOrDefaultAsync(scope => scope.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Scope>> GetScopesByIds(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await context.Scopes
            .Where(scope => ids.Contains(scope.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Scope>> GetScopes(CancellationToken cancellationToken = default)
    {
        return await context.Scopes
            .AsNoTracking()
            .OrderBy(scope => scope.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> NameExists(string name, CancellationToken cancellationToken = default)
    {
        return await context.Scopes
            .AnyAsync(scope => scope.Name == name, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountClientsWithScope(Guid scopeId, CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .CountAsync(
                client => client.AllowedScopes.Any(scope => scope.Id == scopeId)
                    || client.ServedScopes.Any(scope => scope.Id == scopeId),
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteScope(Scope scope, CancellationToken cancellationToken = default)
    {
        // client_scopes and client_served_scopes are removed by the database (cascade delete).
        context.Scopes.Remove(scope);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
