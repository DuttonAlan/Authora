// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Scopes.Create;

/// <summary>
///     The Service to create a new Scope.
/// </summary>
/// <param name="scopeRepository">An Instance of a IScopeRepository to create a new Scope.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateScopeService(
    IScopeRepository scopeRepository,
    ILogger<CreateScopeService> logger)
{
    /// <summary>
    ///     Creates a new Scope.
    /// </summary>
    /// <param name="request">The request to create the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Scope.</returns>
    /// <exception cref="ConflictException">Thrown when the Name is already used by another Scope.</exception>
    public async Task<Guid> ExecuteAsync(
        CreateScopeRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = Scope.Create(request.Name, request.DisplayName, request.Description);

        if (await scopeRepository.NameExists(scope.Name, cancellationToken))
        {
            throw new ConflictException("The Name is already used by another Scope.");
        }

        await scopeRepository.CreateScope(scope, cancellationToken);

        logger.ScopeCreated(scope.Id, scope.Name);

        return scope.Id;
    }
}
