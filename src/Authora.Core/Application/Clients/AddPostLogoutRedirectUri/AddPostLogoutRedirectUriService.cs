// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.AddPostLogoutRedirectUri;

/// <summary>
///     The Service to register a Post-logout Redirect URI for a Client.
/// </summary>
/// <remarks>
///     Like Redirect URIs, no Post-logout Redirect URI can be added to a first-party Client. No code or token is sent
///     there, but the Client Id of the admin interface is public: a URI of one's own would turn its logout into an
///     open redirect on Authora's origin.
/// </remarks>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class AddPostLogoutRedirectUriService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<AddPostLogoutRedirectUriService> logger)
{
    /// <summary>
    ///     Registers a Post-logout Redirect URI for a Client. Does nothing if the URI is already registered.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="request">The request with the Post-logout Redirect URI.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Post-logout Redirect URI is registered.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the URI is not a valid Redirect URI.</exception>
    /// <exception cref="ConflictException">Thrown when the Client is first-party.</exception>
    public async Task ExecuteAsync(
        Guid id,
        AddPostLogoutRedirectUriRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.IsPostLogoutRedirectUriAllowed(request.Uri))
        {
            return;
        }

        // Changing a first-party Client needs the Permission to manage first-party Clients, see AddRedirectUriService.
        if (client.IsFirstParty)
        {
            throw new ConflictException(
                $"Client '{id}' is first-party, so no Post-logout Redirect URI can be added. Mark it as third-party first.");
        }

        client.AddPostLogoutRedirectUri(request.Uri);
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.PostLogoutRedirectUriAdded, client.Id, request.Uri));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.PostLogoutRedirectUriAdded(client.Id, request.Uri);
    }
}
