// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Disable;

/// <summary>
///     The Service to disable a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to disable a Client.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to revoke the tokens of the Client.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class DisableClientService(
    IClientRepository clientRepository,
    ITokenRepository tokenRepository,
    TimeProvider timeProvider,
    ILogger<DisableClientService> logger)
{
    /// <summary>
    ///     Disables a Client, so it can no longer obtain new tokens, and revokes all of its Grants, so its Access and
    ///     Refresh Tokens become invalid. Enabling the Client again does not restore them.
    ///     Does nothing if the Client is already disabled.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is disabled.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.Status == ClientStatus.Disabled)
        {
            return;
        }

        // Revoke first: if saving the status fails afterwards, the Client stays enabled but has to get new tokens,
        // which is safe. The other order could leave a disabled Client with valid tokens that come back on enabling.
        var revokedGrants = await tokenRepository.RevokeGrantsOfClient(client.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        client.Disable();

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ClientDisabled(client.Id, client.Name);
        logger.ClientGrantsRevoked(client.Id, revokedGrants);
    }
}
