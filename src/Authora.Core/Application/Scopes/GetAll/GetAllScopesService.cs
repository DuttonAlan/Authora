// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Scopes.GetAll;

/// <summary>
///     The Service to get all Scopes.
/// </summary>
/// <param name="scopeRepository">An Instance of a IScopeRepository to get all Scopes.</param>
public sealed class GetAllScopesService(IScopeRepository scopeRepository)
{
    /// <summary>
    ///     Gets all Scopes ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Scopes.</returns>
    public async Task<IReadOnlyList<GetAllScopesResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var scopes = await scopeRepository.GetScopes(cancellationToken);

        return scopes
            .Select(scope => new GetAllScopesResponse(
                scope.Id,
                scope.Name,
                scope.DisplayName,
                scope.Description,
                scope.IsSystem))
            .ToList();
    }
}
