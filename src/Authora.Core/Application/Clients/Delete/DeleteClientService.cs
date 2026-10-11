// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Delete;

/// <summary>
///     The Service to delete a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to delete a Client.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteClientService(
    IClientRepository clientRepository,
    ILogger<DeleteClientService> logger)
{
    /// <summary>
    ///     Deletes a Client based on the Id, including its Secrets and Redirect URIs.
    ///     Does nothing if the Client does not exist.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is deleted.</returns>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken);

        if (client is null)
        {
            return;
        }

        await clientRepository.DeleteClient(client, cancellationToken);

        logger.ClientDeleted(client.Id, client.Name);
    }
}
