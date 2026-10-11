// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Scopes.Get;

/// <summary>
///     The Service to get a Scope.
/// </summary>
/// <param name="scopeRepository">An Instance of a IScopeRepository to get a Scope.</param>
public sealed class GetScopeService(IScopeRepository scopeRepository)
{
    /// <summary>
    ///     Gets a Scope based on the Id.
    /// </summary>
    /// <param name="id">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Scope, or null if the Scope does not exist.</returns>
    public async Task<GetScopeResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scope = await scopeRepository.GetScopeById(id, cancellationToken);

        if (scope is null)
        {
            return null;
        }

        return new GetScopeResponse(
            scope.Id,
            scope.Name,
            scope.DisplayName,
            scope.Description,
            scope.IsSystem,
            scope.CreatedAt,
            scope.ModifiedAt);
    }
}
