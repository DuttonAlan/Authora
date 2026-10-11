// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Clients.GetAll;

/// <summary>
///     The Service to get all Clients.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to get all Clients.</param>
public sealed class GetAllClientsService(IClientRepository clientRepository)
{
    /// <summary>
    ///     Gets all Clients ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Clients.</returns>
    public async Task<IReadOnlyList<GetAllClientsResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var clients = await clientRepository.GetClients(cancellationToken);

        return clients
            .Select(client => new GetAllClientsResponse(client.Id, client.Name, client.Type, client.Status))
            .ToList();
    }
}
