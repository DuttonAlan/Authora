// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.RevokeSecret;

/// <summary>
///     The Service to revoke a Secret of a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="logger">The Logger to record the revocation.</param>
public sealed class RevokeClientSecretService(
    IClientRepository clientRepository,
    TimeProvider timeProvider,
    ILogger<RevokeClientSecretService> logger)
{
    /// <summary>
    ///     Revokes a Secret of a Client, so it can no longer be used. Revoking an already revoked Secret keeps its
    ///     original revocation time. The Secret is kept for history.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="secretId">Id of the Secret.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Secret is revoked.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client or the Secret does not exist.</exception>
    public async Task ExecuteAsync(
        Guid clientId,
        Guid secretId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        var secret = client.Secrets.FirstOrDefault(secret => secret.Id == secretId)
            ?? throw new NotFoundException($"Secret '{secretId}' was not found.");

        if (secret.RevokedAt is not null)
        {
            return;
        }

        client.RevokeSecret(secret.Id, timeProvider.GetUtcNow().UtcDateTime);

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.SecretRevoked(client.Id, secret.Id);
    }
}
