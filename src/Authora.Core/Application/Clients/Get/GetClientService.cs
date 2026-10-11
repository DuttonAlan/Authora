// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Clients.Get;

/// <summary>
///     The Service to get a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to get a Client.</param>
/// <param name="timeProvider">The TimeProvider to decide whether Secrets are active.</param>
public sealed class GetClientService(
    IClientRepository clientRepository,
    TimeProvider timeProvider)
{
    /// <summary>
    ///     Gets a Client based on the Id, including its Redirect URIs, Scopes and Secret metadata.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Client, or null if the Client does not exist.</returns>
    public async Task<GetClientResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken);

        if (client is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var scopes = client.AllowedScopes
            .OrderBy(scope => scope.Name, StringComparer.Ordinal)
            .Select(scope => new ClientScopeResponse(scope.Id, scope.Name))
            .ToList();

        var secrets = client.Secrets
            .OrderBy(secret => secret.CreatedAt)
            .Select(secret => new ClientSecretResponse(
                secret.Id,
                secret.CreatedAt,
                secret.ExpiresAt,
                secret.RevokedAt,
                secret.IsActive(now)))
            .ToList();

        return new GetClientResponse(
            client.Id,
            client.Name,
            client.Type,
            client.Status,
            GrantTypeNames.ToNames(client.AllowedGrantTypes),
            client.AccessTokenTTL,
            client.RefreshTokenTTL,
            client.RedirectUris.Select(redirectUri => redirectUri.Value).Order(StringComparer.Ordinal).ToList(),
            scopes,
            secrets,
            client.CreatedAt,
            client.ModifiedAt);
    }
}
