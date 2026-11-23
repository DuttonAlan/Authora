// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Session operations.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    ///     Create a new Session. Also saves all other tracked changes, e.g. to the signed-in User.
    /// </summary>
    /// <param name="session">The Session to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is saved.</returns>
    public Task CreateSession(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Session by the hash of its token.
    /// </summary>
    /// <param name="tokenHash">The hash of the Session token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Session, or null if no Session has this token.</returns>
    public Task<Session?> GetSessionByTokenHash(SecretHash tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Session by its Id.
    /// </summary>
    /// <param name="id">Id of the Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Session, or null if it does not exist (anymore).</returns>
    public Task<Session?> GetSessionById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes a Session in a single atomic update, but only if its Security Stamp differs from the one its User has
    ///     in the database at that moment and it is not revoked yet. Tracked Sessions are not updated.
    /// </summary>
    /// <remarks>
    ///     Unlike revoking a loaded Session, this never revokes a Session that a password change renewed after it was
    ///     loaded.
    /// </remarks>
    /// <param name="id">Id of the Session.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call revoked the Session.</returns>
    public Task<bool> RevokeSessionIfSecurityStampChanged(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts a transaction, or joins the one the request already has, e.g. that of the
    ///     <see cref="Application.Authorization.PrivilegeGuard"/>. Revoking Sessions and checking a Session before
    ///     creating a Grant in it need one.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction; committing a joined one does nothing, its owner commits it.</returns>
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Locks a Session against being revoked until the transaction of the request ends, so a Grant can be created
    ///     in it without missing a revocation that runs at the same time: the revocation waits, and then sees the Grant.
    ///     Waits while a revocation of the Session is not committed yet. Load the Session afterwards.
    /// </summary>
    /// <param name="id">Id of the Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is locked, or if it does not exist.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the request has no transaction.</exception>
    public Task LockSession(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes a Session in a single atomic update, unless it is already revoked. Tracked Sessions are not updated.
    /// </summary>
    /// <remarks>
    ///     Of several concurrent calls for the same Session, exactly one revokes it.
    /// </remarks>
    /// <param name="id">Id of the Session.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call revoked the Session.</returns>
    public Task<bool> RevokeSession(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes all Sessions of a User that are not revoked yet, in a single atomic update. Tracked Sessions are not
    ///     updated.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="exceptSessionId">Id of a Session that stays valid, e.g. the one the User changes the password in, or null.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Ids of the Sessions this call revoked.</returns>
    public Task<IReadOnlyList<Guid>> RevokeSessionsOfUser(
        Guid userId,
        Guid? exceptSessionId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the stored values of a tracked Session again, e.g. after another request changed it.
    /// </summary>
    /// <param name="session">The tracked Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is reloaded.</returns>
    public Task ReloadSession(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Sessions.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    /// <exception cref="Application.Exceptions.ConflictException">
    ///     Thrown when a Session got a new token since it was loaded, e.g. by a step-up. The outdated Session is no
    ///     longer tracked, so loading it again returns the current values.
    /// </exception>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
