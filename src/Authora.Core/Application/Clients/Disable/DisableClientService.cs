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
/// <param name="logger">The Logger to record the change.</param>
public sealed class DisableClientService(
    IClientRepository clientRepository,
    ILogger<DisableClientService> logger)
{
    /// <summary>
    ///     Disables a Client, so it can no longer obtain new tokens. Does nothing if the Client is already disabled.
    /// </summary>
    /// <remarks>
    ///     Once Refresh Tokens exist, the Refresh Tokens of the Client must also be revoked here (TD-002).
    /// </remarks>
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

        client.Disable();

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ClientDisabled(client.Id, client.Name);
    }
}
