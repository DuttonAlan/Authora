// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.CreateSecret;

/// <summary>
///     The Service to create a new Secret for a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="clientSecretIssuer">The issuer for the new Secret.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateClientSecretService(
    IClientRepository clientRepository,
    ClientSecretIssuer clientSecretIssuer,
    ILogger<CreateClientSecretService> logger)
{
    /// <summary>
    ///     Creates a new Secret for a Confidential Client. Existing Secrets stay active, which allows rotation.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="request">The request to create the Secret.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new Secret including its plain-text value, which is only returned once.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    /// <exception cref="DomainValidationException">
    ///     Thrown when the expiry is not in the future or later than <see cref="ClientSecret.MaxLifetime"/> from now.
    /// </exception>
    /// <exception cref="ConflictException">Thrown when the Client is not a Confidential Client.</exception>
    public async Task<IssuedClientSecretResponse> ExecuteAsync(
        Guid id,
        CreateClientSecretRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.Type != ClientType.Confidential)
        {
            throw new ConflictException("Only Confidential Clients can have Secrets.");
        }

        var secret = clientSecretIssuer.Issue(client, request.ExpiresAt?.UtcDateTime);

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.SecretCreated(client.Id, secret.Id, secret.ExpiresAt);

        return secret;
    }
}
