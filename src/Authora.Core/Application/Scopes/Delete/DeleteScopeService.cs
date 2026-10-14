// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Scopes.Delete;

/// <summary>
///     The Service to delete a Scope.
/// </summary>
/// <param name="scopeRepository">An Instance of a IScopeRepository to delete a Scope.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteScopeService(
    IScopeRepository scopeRepository,
    IAuditLog auditLog,
    ILogger<DeleteScopeService> logger)
{
    /// <summary>
    ///     Deletes a Scope based on the Id. Does nothing if the Scope does not exist.
    /// </summary>
    /// <param name="id">Id of the Scope.</param>
    /// <param name="force">True to delete the Scope even if Clients use it, which removes it from those Clients.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is deleted.</returns>
    /// <exception cref="ConflictException">Thrown when the Scope is a System Scope, or Clients use it and <paramref name="force"/> is false.</exception>
    public async Task ExecuteAsync(
        Guid id,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var scope = await scopeRepository.GetScopeById(id, cancellationToken);

        if (scope is null)
        {
            return;
        }

        if (scope.IsSystem)
        {
            throw new ConflictException("A System Scope cannot be deleted.");
        }

        var clientCount = await scopeRepository.CountClientsWithScope(scope.Id, cancellationToken);

        if (clientCount > 0 && !force)
        {
            throw new ConflictException(
                $"The Scope is used by {clientCount} Clients. Remove it from these Clients first, or delete it with force.");
        }

        auditLog.Record(AuditEntry.ForScope(
            AuditEventTypes.Scopes.Deleted,
            scope.Id,
            $"Name '{scope.Name}', removed from {clientCount} Client(s)"));

        await scopeRepository.DeleteScope(scope, cancellationToken);

        logger.ScopeDeleted(scope.Id, scope.Name, clientCount);
    }
}
