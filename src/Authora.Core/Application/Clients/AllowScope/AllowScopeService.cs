// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.AllowScope;

/// <summary>
///     The Service to allow a Client to request a Scope.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="scopeRepository">An Instance of a IScopeRepository to get the Scope.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class AllowScopeService(
    IClientRepository clientRepository,
    IScopeRepository scopeRepository,
    IAuditLog auditLog,
    ILogger<AllowScopeService> logger)
{
    /// <summary>
    ///     Allows a Client to request a Scope. Does nothing if the Scope is already allowed.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="scopeId">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is allowed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client or the Scope does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the Client is first-party.</exception>
    public async Task ExecuteAsync(
        Guid clientId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        if (client.AllowedScopes.Any(scope => scope.Id == scopeId))
        {
            return;
        }

        // A first-party Client obtains its Scopes without asking the User. Marking it as third-party first needs the
        // Permission to manage first-party Clients.
        if (client.IsFirstParty)
        {
            throw new ConflictException(
                $"Client '{clientId}' is first-party, so no Scope can be allowed. Mark it as third-party first.");
        }

        var scope = await scopeRepository.GetScopeById(scopeId, cancellationToken)
            ?? throw new NotFoundException($"Scope '{scopeId}' was not found.");

        client.AllowScope(scope);
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.ScopeAllowed, client.Id, $"Scope '{scope.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ScopeAllowed(client.Id, scope.Id, scope.Name);
    }
}
