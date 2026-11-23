// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the outbox of back-channel logout notifications
///     (<see cref="Domain.Entities.BackchannelLogoutNotification"/>).
/// </summary>
public interface IBackchannelLogoutRepository
{
    /// <summary>
    ///     Writes one notification for every Client with a Back-Channel Logout URI that has a Grant approved in one of
    ///     the given Sessions, in a single statement that runs right away. Call it in the transaction that revokes the
    ///     Sessions, so the notifications are only stored together with the revocation.
    /// </summary>
    /// <param name="sessionIds">Ids of the Sessions that ended.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of notifications written.</returns>
    public Task<int> EnqueueForSessions(IReadOnlyCollection<Guid> sessionIds, DateTime now, CancellationToken cancellationToken = default);
}
