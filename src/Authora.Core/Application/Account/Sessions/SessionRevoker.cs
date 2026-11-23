// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.BackchannelLogout;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account.Sessions;

/// <summary>
///     Ends Sessions explicitly: revokes them, revokes the Grants approved in them (with their Access and Refresh
///     Tokens) and writes a back-channel logout notification for every Client that received tokens in them and has a
///     Back-Channel Logout URI (OpenID Connect Back-Channel Logout 1.0).
/// </summary>
/// <remarks>
///     Used by every path that ends Sessions: signing out, signing a User out everywhere, password changes and resets,
///     removing a second factor, deactivating and deleting a User. Call <see cref="BeginAsync"/> before the change and
///     commit the transaction after saving it, so the revocation, the notifications, the change and its audit event
///     are stored together or not at all. Sessions that expire or stay unused end without a notification.
/// </remarks>
/// <param name="sessionRepository">An Instance of a ISessionRepository to revoke the Sessions.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to revoke the Grants.</param>
/// <param name="backchannelLogoutRepository">An Instance of a IBackchannelLogoutRepository to write the notifications.</param>
/// <param name="logger">The Logger to record the revocation.</param>
public sealed class SessionRevoker(
    ISessionRepository sessionRepository,
    ITokenRepository tokenRepository,
    IBackchannelLogoutRepository backchannelLogoutRepository,
    ILogger<SessionRevoker> logger)
{
    /// <summary>
    ///     Starts the transaction the revocation runs in, or joins the one the request already has.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction. Commit it after saving the change.</returns>
    public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        => sessionRepository.BeginTransactionAsync(cancellationToken);

    /// <summary>
    ///     Ends a single Session, e.g. when the User signs out. Does nothing if it is already revoked.
    /// </summary>
    /// <param name="userId">Id of the User of the Session.</param>
    /// <param name="sessionId">Id of the Session.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call revoked the Session, false if it was already revoked.</returns>
    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken cancellationToken = default)
    {
        if (!await sessionRepository.RevokeSession(sessionId, now, cancellationToken))
        {
            return false;
        }

        await EndAsync(userId, [sessionId], now, cancellationToken);

        return true;
    }

    /// <summary>
    ///     Ends all Sessions of a User that are not revoked yet, optionally except one.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="exceptSessionId">Id of a Session that stays valid, e.g. the one the User changes the password in, or null.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of Sessions this call revoked.</returns>
    public async Task<int> RevokeSessionsOfUserAsync(
        Guid userId,
        Guid? exceptSessionId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var sessionIds = await sessionRepository.RevokeSessionsOfUser(userId, exceptSessionId, now, cancellationToken);

        if (sessionIds.Count > 0)
        {
            await EndAsync(userId, sessionIds, now, cancellationToken);
        }

        return sessionIds.Count;
    }

    private async Task EndAsync(Guid userId, IReadOnlyCollection<Guid> sessionIds, DateTime now, CancellationToken cancellationToken)
    {
        var grants = await tokenRepository.RevokeGrantsOfSessions(sessionIds, now, cancellationToken);
        var notifications = await backchannelLogoutRepository.EnqueueForSessions(sessionIds, now, cancellationToken);

        logger.SessionsEnded(userId, sessionIds.Count, grants, notifications);
    }
}
