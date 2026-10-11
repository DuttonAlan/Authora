// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Register;

/// <summary>
///     The Service to register a new Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to create the Client.</param>
/// <param name="scopeRepository">An Instance of a IScopeRepository to get the allowed Scopes.</param>
/// <param name="clientSecretIssuer">The issuer for the first Secret of a Confidential Client.</param>
/// <param name="logger">The Logger to record the registration.</param>
public sealed class RegisterClientService(
    IClientRepository clientRepository,
    IScopeRepository scopeRepository,
    ClientSecretIssuer clientSecretIssuer,
    ILogger<RegisterClientService> logger)
{
    /// <summary>
    ///     Registers a new Client. A Confidential Client also gets its first Secret, which never expires.
    /// </summary>
    /// <param name="request">The request to register the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the Client and, for a Confidential Client, its first plain-text Secret.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is invalid or a Scope does not exist.</exception>
    public async Task<RegisterClientResponse> ExecuteAsync(
        RegisterClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = Client.Create(
            request.Name,
            request.Type ?? throw new ArgumentException("Type is required.", nameof(request)));

        if (request.GrantTypes is not null)
        {
            client.SetAllowedGrantTypes(GrantTypeNames.Parse(request.GrantTypes));
        }

        client.SetTokenLifetimes(
            request.AccessTokenTTL ?? client.AccessTokenTTL,
            request.RefreshTokenTTL ?? client.RefreshTokenTTL);

        foreach (var redirectUri in request.RedirectUris ?? [])
        {
            client.AddRedirectUri(redirectUri);
        }

        if (request.ScopeIds is { Count: > 0 })
        {
            var scopeIds = request.ScopeIds.Distinct().ToList();
            var scopes = await scopeRepository.GetScopesByIds(scopeIds, cancellationToken);

            var missingScopeIds = scopeIds.Except(scopes.Select(scope => scope.Id)).ToList();

            if (missingScopeIds.Count > 0)
            {
                throw new ArgumentException(
                    $"Scopes not found: {string.Join(", ", missingScopeIds)}.",
                    nameof(request));
            }

            foreach (var scope in scopes)
            {
                client.AllowScope(scope);
            }
        }

        var secret = client.Type == ClientType.Confidential
            ? clientSecretIssuer.Issue(client, expiresAt: null)
            : null;

        await clientRepository.CreateClient(client, cancellationToken);

        logger.ClientRegistered(client.Id, client.Name, client.Type);

        if (secret is not null)
        {
            logger.SecretCreated(client.Id, secret.Id, secret.ExpiresAt);
        }

        return new RegisterClientResponse(client.Id, secret);
    }
}
