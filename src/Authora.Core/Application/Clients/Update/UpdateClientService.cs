// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Update;

/// <summary>
///     The Service to update an existing Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to update a Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateClientService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
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
    /// <exception cref="ConflictException">Thrown when client_credentials should be allowed for a first-party Client.</exception>
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
            var grantTypes = GrantTypeNames.Parse(request.GrantTypes);

            // With a Secret, client_credentials would give tokens for the Scopes of a first-party Client without any
            // User, so allowing it needs the Permission to manage first-party Clients, see AddRedirectUriService.
            // Keeping or removing it is always allowed.
            if (client.IsFirstParty
                && grantTypes.HasFlag(GrantTypes.ClientCredentials)
                && !client.IsGrantTypeAllowed(GrantTypes.ClientCredentials))
            {
                throw new ConflictException(
                    $"Client '{id}' is first-party, so client_credentials cannot be allowed. Mark it as third-party first.");
            }

            client.SetAllowedGrantTypes(grantTypes);
        }

        // Both lifetimes are validated together, because the Refresh Token TTL must exceed the Access Token TTL.
        client.SetTokenLifetimes(
            request.AccessTokenTTL ?? client.AccessTokenTTL,
            request.RefreshTokenTTL ?? client.RefreshTokenTTL);

        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.Updated, client.Id, $"Name '{client.Name}'"));

        // Only changed values are written; ModifiedAt is set when saving.
        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ClientUpdated(client.Id, client.Name);
    }
}
