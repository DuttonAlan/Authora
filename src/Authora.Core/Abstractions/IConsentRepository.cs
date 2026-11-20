// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the Consents of Users.
/// </summary>
public interface IConsentRepository
{
    /// <summary>
    ///     Starts the transaction for changing the Consent of a User for a Client, and locks it until the transaction
    ///     ends, so that concurrent approvals and revocations of the same Consent run one after the other. Joins the
    ///     transaction of the request if there is one; the lock is then held until that transaction ends.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction. Committing a joined transaction is left to its owner.</returns>
    public Task<ITransaction> BeginConsentChangeAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the Consent of a User for a Client, with its Scopes.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Consent, or null if the User has not approved the Client.</returns>
    public Task<Consent?> GetConsent(Guid userId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Consents of a User with their Client and Scopes, ordered by the Name of the Client. Not tracked.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Consents, or an empty list.</returns>
    public Task<IReadOnlyList<Consent>> GetConsentsOfUser(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new Consent. It is saved with <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="consent">The Consent.</param>
    public void AddConsent(Consent consent);

    /// <summary>
    ///     Removes a tracked Consent. It is deleted with <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="consent">The Consent.</param>
    public void RemoveConsent(Consent consent);

    /// <summary>
    ///     Deletes all Consents of a User right away, e.g. because the User is deleted.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of deleted Consents.</returns>
    public Task<int> DeleteConsentsOfUser(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes to tracked Consents, together with the audit events of the request.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
