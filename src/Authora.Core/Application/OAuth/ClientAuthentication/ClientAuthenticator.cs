// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth.ClientAuthentication;

/// <summary>
///     Authenticates the Client of a request to the token, revocation or introspection endpoint.
/// </summary>
/// <remarks>
///     Confidential Clients must send one of their active Secrets. Public Clients only send their <c>client_id</c>
///     and must not send a Secret. The Status of the Client is not checked here, because a disabled Client may still
///     revoke its tokens.
/// </remarks>
/// <param name="clientRepository">An Instance of a IClientRepository to load the Client.</param>
/// <param name="secretHasher">The hasher to verify the Secret.</param>
/// <param name="timeProvider">The TimeProvider to check the expiry of the Secrets.</param>
/// <param name="logger">The Logger to record failed authentications.</param>
public sealed class ClientAuthenticator(
    IClientRepository clientRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<ClientAuthenticator> logger)
{
    /// <summary>
    ///     Authenticates the Client.
    /// </summary>
    /// <param name="credentials">The credentials the Client sent.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The authenticated, tracked Client.</returns>
    /// <exception cref="OAuthException">Thrown with <see cref="OAuthErrors.InvalidClient"/> when the authentication fails.</exception>
    public async Task<Client> AuthenticateAsync(ClientCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (!Guid.TryParseExact(credentials.ClientId, "D", out var clientId))
        {
            throw Fail(credentials.ClientId, "the client_id is missing or malformed");
        }

        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw Fail(credentials.ClientId, "the Client does not exist");

        if (client.Type == ClientType.Public)
        {
            // A Public Client has no Secrets, so a sent Secret can never be valid.
            if (credentials.ClientSecret is not null)
            {
                throw Fail(credentials.ClientId, "a Public Client sent a Secret");
            }

            return client;
        }

        if (string.IsNullOrEmpty(credentials.ClientSecret))
        {
            throw Fail(credentials.ClientId, "a Confidential Client sent no Secret");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var isValid = false;

        // Check every active Secret instead of stopping at the first match, so the time does not reveal which one matched.
        foreach (var secret in client.Secrets.Where(secret => secret.IsActive(now)))
        {
            isValid |= secretHasher.Verify(credentials.ClientSecret, secret.SecretHash);
        }

        if (!isValid)
        {
            throw Fail(credentials.ClientId, "the Secret is wrong, expired or revoked");
        }

        return client;
    }

    private OAuthException Fail(string? clientId, string reason)
    {
        logger.ClientAuthenticationFailed(clientId ?? "(none)", reason);

        return new OAuthException(OAuthErrors.InvalidClient, "Client authentication failed.");
    }
}
