// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.ServeScope;

/// <summary>
///     The Service to make a Client serve a Scope as a resource server.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="scopeRepository">An Instance of a IScopeRepository to get the Scope.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class ServeScopeService(
    IClientRepository clientRepository,
    IScopeRepository scopeRepository,
    ILogger<ServeScopeService> logger)
{
    /// <summary>
    ///     Makes a Client serve a Scope, so it can introspect Access Tokens with this Scope. Does nothing if the Scope
    ///     is already served.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="scopeId">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is served.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client or the Scope does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the Client is not a Confidential Client.</exception>
    public async Task ExecuteAsync(
        Guid clientId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        if (client.Type != ClientType.Confidential)
        {
            throw new ConflictException("Only Confidential Clients can serve Scopes, because only they can introspect tokens.");
        }

        if (client.ServedScopes.Any(scope => scope.Id == scopeId))
        {
            return;
        }

        var scope = await scopeRepository.GetScopeById(scopeId, cancellationToken)
            ?? throw new NotFoundException($"Scope '{scopeId}' was not found.");

        client.ServeScope(scope);

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ScopeServed(client.Id, scope.Id, scope.Name);
    }
}
