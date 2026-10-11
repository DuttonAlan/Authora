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
    ///     Saves all changes made to tracked Sessions.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
