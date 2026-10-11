// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.CreateSecret;

/// <summary>
///     The Service to create a new Secret for a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="clientSecretIssuer">The issuer for the new Secret.</param>
/// <param name="timeProvider">The TimeProvider to check the expiry.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateClientSecretService(
    IClientRepository clientRepository,
    ClientSecretIssuer clientSecretIssuer,
    TimeProvider timeProvider,
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
    /// <exception cref="ArgumentException">Thrown when the expiry is not in the future.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Client is not a Confidential Client.</exception>
    public async Task<IssuedClientSecretResponse> ExecuteAsync(
        Guid id,
        CreateClientSecretRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        var expiresAt = request.ExpiresAt?.UtcDateTime;

        if (expiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new ArgumentException("The expiry of the Secret must be in the future.", nameof(request));
        }

        var secret = clientSecretIssuer.Issue(client, expiresAt);

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.SecretCreated(client.Id, secret.Id, secret.ExpiresAt);

        return secret;
    }
}
