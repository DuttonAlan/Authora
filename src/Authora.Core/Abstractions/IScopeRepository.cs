// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Scope operations.
/// </summary>
/// <remarks>
///     Scopes are changed by loading them tracked via <see cref="GetScopeById"/>, changing them
///     through their methods and calling <see cref="SaveChangesAsync"/>.
/// </remarks>
public interface IScopeRepository
{
    /// <summary>
    ///     Create a new Scope.
    /// </summary>
    /// <param name="scope">The Scope to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Scope.</returns>
    public Task<Guid> CreateScope(Scope scope, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Scope by Id.
    /// </summary>
    /// <param name="id">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Scope, or null if the Scope does not exist.</returns>
    public Task<Scope?> GetScopeById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the Scopes with the given Ids. Ids without a matching Scope are skipped.
    /// </summary>
    /// <param name="ids">Ids of the Scopes.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Scopes that exist.</returns>
    public Task<IReadOnlyList<Scope>> GetScopesByIds(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Scopes ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Scopes.</returns>
    public Task<IReadOnlyList<Scope>> GetScopes(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a Scope with the given Name exists. Names are compared case-sensitively.
    /// </summary>
    /// <param name="name">The Name of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Name is already used.</returns>
    public Task<bool> NameExists(string name, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the Clients that are allowed to request the given Scope.
    /// </summary>
    /// <param name="scopeId">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of Clients using the Scope.</returns>
    public Task<int> CountClientsWithScope(Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a Scope. It is also removed from the allowed Scopes of all Clients.
    /// </summary>
    /// <param name="scope">The tracked Scope to delete.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Scope is deleted.</returns>
    public Task DeleteScope(Scope scope, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Scopes.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
