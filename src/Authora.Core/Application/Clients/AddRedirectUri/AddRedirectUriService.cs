// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.AddRedirectUri;

/// <summary>
///     The Service to register a Redirect URI for a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class AddRedirectUriService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<AddRedirectUriService> logger)
{
    /// <summary>
    ///     Registers a Redirect URI for a Client. Does nothing if the URI is already registered.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="request">The request with the Redirect URI.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Redirect URI is registered.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the URI is not a valid Redirect URI.</exception>
    public async Task ExecuteAsync(
        Guid id,
        AddRedirectUriRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.IsRedirectUriAllowed(request.Uri))
        {
            return;
        }

        client.AddRedirectUri(request.Uri);
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.RedirectUriAdded, client.Id, request.Uri));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.RedirectUriAdded(client.Id, request.Uri);
    }
}
