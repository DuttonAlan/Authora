// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Update;

/// <summary>
///     The Service to update an existing Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to update a Client.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateClientService(
    IClientRepository clientRepository,
    ILogger<UpdateClientService> logger)
{
    /// <summary>
    ///     Updates the Name, Grant Types and token lifetimes of an existing Client.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="request">The request to update the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is invalid.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (request.Name is not null)
        {
            client.SetName(request.Name);
        }

        if (request.GrantTypes is not null)
        {
            client.SetAllowedGrantTypes(GrantTypeNames.Parse(request.GrantTypes));
        }

        // Both lifetimes are validated together, because the Refresh Token TTL must exceed the Access Token TTL.
        client.SetTokenLifetimes(
            request.AccessTokenTTL ?? client.AccessTokenTTL,
            request.RefreshTokenTTL ?? client.RefreshTokenTTL);

        // Only changed values are written; ModifiedAt is set when saving.
        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ClientUpdated(client.Id, client.Name);
    }
}
