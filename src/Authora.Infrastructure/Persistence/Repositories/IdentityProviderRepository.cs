// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the identity providers.</param>
public sealed class IdentityProviderRepository(AuthoraContext context) : IIdentityProviderRepository
{
    /// <inheritdoc/>
    public async Task CreateProvider(IdentityProvider provider, CancellationToken cancellationToken = default)
    {
        context.IdentityProviders.Add(provider);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider?> GetProviderById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.IdentityProviders
            .FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IdentityProvider?> GetProviderBySlug(string slug, CancellationToken cancellationToken = default)
    {
        return await context.IdentityProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(provider => provider.Slug == slug, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityProvider>> GetProviders(bool enabledOnly, CancellationToken cancellationToken = default)
    {
        return await context.IdentityProviders
            .AsNoTracking()
            .Where(provider => !enabledOnly || provider.IsEnabled)
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SlugExists(string slug, CancellationToken cancellationToken = default)
    {
        return await context.IdentityProviders
            .AnyAsync(provider => provider.Slug == slug, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountExternalIdentities(Guid providerId, CancellationToken cancellationToken = default)
    {
        return await context.ExternalIdentities
            .CountAsync(identity => identity.ProviderId == providerId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteProvider(IdentityProvider provider, CancellationToken cancellationToken = default)
    {
        context.IdentityProviders.Remove(provider);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // An identity was linked after the check of the service.
            throw new ConflictException("An account was linked through the identity provider in the meantime.", exception);
        }
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
