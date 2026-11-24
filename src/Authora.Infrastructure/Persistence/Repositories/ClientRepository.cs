// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Clients.</param>
public sealed class ClientRepository(AuthoraContext context) : IClientRepository
{
    /// <inheritdoc/>
    public async Task<Guid> CreateClient(Client client, CancellationToken cancellationToken = default)
    {
        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        return client.Id;
    }

    /// <inheritdoc/>
    public async Task<Client?> GetClientById(Guid id, CancellationToken cancellationToken = default)
    {
        // Redirect URIs are owned by the Client and loaded automatically.
        return await context.Clients
            .AsSplitQuery()
            .Include(client => client.Secrets)
            .Include(client => client.AllowedScopes)
            .Include(client => client.ServedScopes)
            .FirstOrDefaultAsync(client => client.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Client>> GetClients(CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .AsNoTracking()
            .OrderBy(client => client.Name)
            .ThenBy(client => client.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetRedirectUrisOfEnabledClients(CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .AsNoTracking()
            .Where(client => client.Status == ClientStatus.Enabled)
            .SelectMany(client => client.RedirectUris)
            .Select(redirectUri => redirectUri.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Guid?> GetEnabledPublicClientIdByRedirectUri(string redirectUri, CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .AsNoTracking()
            .Where(client => client.Status == ClientStatus.Enabled && client.Type == ClientType.Public)
            .Where(client => client.RedirectUris.Any(uri => uri.Value == redirectUri))
            .OrderBy(client => client.CreatedAt)
            .ThenBy(client => client.Id)
            .Select(client => (Guid?)client.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Client?> GetEnabledApiByResourceIdentifier(string resourceIdentifier, CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .AsNoTracking()
            .Include(client => client.ServedScopes)
            .FirstOrDefaultAsync(
                client => client.ResourceIdentifier == resourceIdentifier
                    && client.Status == ClientStatus.Enabled
                    && client.Type == ClientType.Confidential,
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsResourceIdentifierTaken(string resourceIdentifier, Guid exceptClientId, CancellationToken cancellationToken = default)
    {
        return await context.Clients
            .AnyAsync(client => client.ResourceIdentifier == resourceIdentifier && client.Id != exceptClientId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Client>> GetClientsWithServedScopes(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Clients
            .AsNoTracking()
            .Include(client => client.ServedScopes)
            .Where(client => ids.Contains(client.Id))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> GetIdsOfClientsServingAnyScope(IReadOnlyCollection<string> scopeNames, CancellationToken cancellationToken = default)
    {
        if (scopeNames.Count == 0)
        {
            return [];
        }

        return await context.Clients
            .AsNoTracking()
            .Where(client => !client.RequiresResourceIndicator)
            .Where(client => client.ServedScopes.Any(scope => scopeNames.Contains(scope.Name)))
            .OrderBy(client => client.Id)
            .Select(client => client.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsClientDisabled(Guid id, CancellationToken cancellationToken = default)
    {
        return !await context.Clients
            .AnyAsync(client => client.Id == id && client.Status == ClientStatus.Enabled, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteClient(Client client, CancellationToken cancellationToken = default)
    {
        // Secrets, Redirect URIs and client_scopes are removed with the Client (cascade delete).
        context.Clients.Remove(client);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
