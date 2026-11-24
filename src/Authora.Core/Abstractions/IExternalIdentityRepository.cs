// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the external identities of Users.
/// </summary>
public interface IExternalIdentityRepository
{
    /// <summary>
    ///     Finds the identity of an account at a provider.
    /// </summary>
    /// <param name="providerId">Id of the provider.</param>
    /// <param name="subject">The Subject at the provider, compared case-sensitively.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked identity, or null if the account is not linked.</returns>
    public Task<ExternalIdentity?> FindIdentity(Guid providerId, string subject, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the identities of a User, ordered by when they were linked.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked identities.</returns>
    public Task<IReadOnlyList<ExternalIdentity>> GetIdentitiesOfUser(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Links an identity. Fails without throwing if the account is already linked or the User already has an
    ///     identity at the provider, also when another request linked it at the same time.
    /// </summary>
    /// <param name="identity">The new identity.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the identity was saved, false if it conflicts with an existing one.</returns>
    public Task<bool> TryCreateIdentity(ExternalIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks identities for removal; they are deleted with the next save.
    /// </summary>
    /// <param name="identities">The tracked identities.</param>
    public void RemoveIdentities(IEnumerable<ExternalIdentity> identities);

    /// <summary>
    ///     Saves all changes made to tracked identities.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
