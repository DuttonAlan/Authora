// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.DisallowScope;

/// <summary>
///     The Service to disallow a Client to request a Scope.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class DisallowScopeService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<DisallowScopeService> logger)
{
    /// <summary>
    ///     Disallows a Client to request a Scope. Does nothing if the Scope is not allowed.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="scopeId">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is disallowed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid clientId,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(clientId, cancellationToken)
            ?? throw new NotFoundException($"Client '{clientId}' was not found.");

        var scope = client.AllowedScopes.FirstOrDefault(scope => scope.Id == scopeId);

        if (scope is null)
        {
            return;
        }

        client.DisallowScope(scope.Id);
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.ScopeDisallowed, client.Id, $"Scope '{scope.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ScopeDisallowed(client.Id, scope.Id, scope.Name);
    }
}
