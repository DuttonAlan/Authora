// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.StopServingScope;

/// <summary>
///     The Service to stop a Client from serving a Scope as a resource server.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class StopServingScopeService(
    IClientRepository clientRepository,
    ILogger<StopServingScopeService> logger)
{
    /// <summary>
    ///     Stops a Client from serving a Scope. Does nothing if the Scope is not served.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="scopeId">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is no longer served.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid clientId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        var scope = client.ServedScopes.FirstOrDefault(scope => scope.Id == scopeId);

        if (scope is null)
        {
            return;
        }

        client.StopServingScope(scope.Id);

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ScopeNoLongerServed(client.Id, scope.Id, scope.Name);
    }
}
