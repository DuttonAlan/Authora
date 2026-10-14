// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.RemoveRedirectUri;

/// <summary>
///     The Service to remove a Redirect URI from a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class RemoveRedirectUriService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<RemoveRedirectUriService> logger)
{
    /// <summary>
    ///     Removes a Redirect URI from a Client. Does nothing if the URI is not registered.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="uri">The Redirect URI to remove, compared exactly (case-sensitive).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Redirect URI is removed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        string uri,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (!client.IsRedirectUriAllowed(uri))
        {
            return;
        }

        client.RemoveRedirectUri(uri);
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.RedirectUriRemoved, client.Id, uri));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.RedirectUriRemoved(client.Id, uri);
    }
}
