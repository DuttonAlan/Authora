// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Client operations.
/// </summary>
/// <remarks>
///     Clients are changed by loading them tracked via <see cref="GetClientById"/>, changing them
///     through their methods and calling <see cref="SaveChangesAsync"/>.
/// </remarks>
public interface IClientRepository
{
    /// <summary>
    ///     Create a new Client, including its Secrets, Redirect URIs and allowed Scopes.
    /// </summary>
    /// <param name="client">The Client to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Client.</returns>
    public Task<Guid> CreateClient(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Client by Id, including its Secrets, Redirect URIs, allowed Scopes and served Scopes.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Client, or null if the Client does not exist.</returns>
    public Task<Client?> GetClientById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Clients ordered by their Name, without their Secrets, Redirect URIs and Scopes.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Clients.</returns>
    public Task<IReadOnlyList<Client>> GetClients(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the Redirect URIs of all enabled Clients.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Redirect URIs, without duplicates.</returns>
    public Task<IReadOnlyList<string>> GetRedirectUrisOfEnabledClients(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the oldest enabled Public Client that has exactly the given Redirect URI.
    /// </summary>
    /// <param name="redirectUri">The Redirect URI, compared character by character.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the Client, or null if there is none.</returns>
    public Task<Guid?> GetEnabledPublicClientIdByRedirectUri(string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the enabled Confidential Client with exactly the given Resource Identifier, including its served Scopes.
    /// </summary>
    /// <param name="resourceIdentifier">The Resource Identifier, compared character by character.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The untracked Client, or null if no enabled API has the identifier.</returns>
    public Task<Client?> GetEnabledApiByResourceIdentifier(string resourceIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether another Client already has the given Resource Identifier.
    /// </summary>
    /// <param name="resourceIdentifier">The Resource Identifier, compared character by character.</param>
    /// <param name="exceptClientId">The Client to ignore, e.g. the one being changed.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if another Client has it.</returns>
    public Task<bool> IsResourceIdentifierTaken(string resourceIdentifier, Guid exceptClientId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the Clients with the given Ids, including their served Scopes. Ids of Clients that no longer exist are
    ///     skipped.
    /// </summary>
    /// <param name="ids">The Ids of the Clients.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The untracked Clients.</returns>
    public Task<IReadOnlyList<Client>> GetClientsWithServedScopes(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the Ids of the Clients that serve at least one of the given Scopes and do not require the
    ///     <c>resource</c> parameter (<see cref="Client.RequiresResourceIndicator"/>): the audience of an Access Token
    ///     requested without <c>resource</c>.
    /// </summary>
    /// <param name="scopeNames">The Scope Names of the token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Ids of the Clients, ordered.</returns>
    public Task<IReadOnlyList<Guid>> GetIdsOfClientsServingAnyScope(IReadOnlyCollection<string> scopeNames, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks in the database whether a Client is disabled, ignoring any tracked (possibly outdated) instance.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Client is disabled or no longer exists.</returns>
    public Task<bool> IsClientDisabled(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a Client, including its Secrets and Redirect URIs.
    /// </summary>
    /// <param name="client">The tracked Client to delete.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is deleted.</returns>
    public Task DeleteClient(Client client, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Clients.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
