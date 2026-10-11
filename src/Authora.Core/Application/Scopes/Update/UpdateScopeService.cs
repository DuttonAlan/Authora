// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Scopes.Update;

/// <summary>
///     The Service to update an existing Scope.
/// </summary>
/// <param name="scopeRepository">An Instance of a IScopeRepository to update a Scope.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateScopeService(
    IScopeRepository scopeRepository,
    ILogger<UpdateScopeService> logger)
{
    /// <summary>
    ///     Updates the Display Name and Description of an existing Scope.
    /// </summary>
    /// <param name="id">Id of the Scope.</param>
    /// <param name="request">The request to update the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the Scope does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = await scopeRepository.GetScopeById(id, cancellationToken)
            ?? throw new NotFoundException($"Scope '{id}' was not found.");

        if (request.DisplayName is not null)
        {
            scope.SetDisplayName(request.DisplayName);
        }

        if (request.Description is not null)
        {
            scope.SetDescription(request.Description);
        }

        // Only changed values are written; ModifiedAt is set when saving.
        await scopeRepository.SaveChangesAsync(cancellationToken);

        logger.ScopeUpdated(scope.Id, scope.Name);
    }
}
