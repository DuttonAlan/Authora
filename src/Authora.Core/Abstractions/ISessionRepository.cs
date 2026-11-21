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
