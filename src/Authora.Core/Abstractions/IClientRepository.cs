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
    ///     Get a Client by Id, including its Secrets, Redirect URIs and allowed Scopes.
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
