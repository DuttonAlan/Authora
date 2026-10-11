// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.Sessions;

/// <summary>
///     The Service to validate the Session token from the Session cookie.
/// </summary>
/// <param name="sessionRepository">An Instance of a ISessionRepository to load the Session.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check the User.</param>
/// <param name="secretHasher">The hasher to look up the Session token.</param>
/// <param name="timeProvider">The TimeProvider for the expiry checks.</param>
/// <param name="options">The sign-in settings with the Session lifetimes.</param>
/// <param name="logger">The Logger to record invalidated Sessions.</param>
public sealed class ValidateSessionService(
    ISessionRepository sessionRepository,
    IUserRepository userRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    ILogger<ValidateSessionService> logger)
{
    /// <summary>
    ///     The activity of a Session is saved at most this often, so not every request writes to the database.
    /// </summary>
    private static readonly TimeSpan MaxActivityUpdateInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Validates a Session token and records the activity of the Session.
    /// </summary>
    /// <param name="sessionToken">The plain-text Session token from the cookie.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Session and its User, or null if the Session is unknown, revoked, expired, idle or its User can no longer sign in.</returns>
    public async Task<AuthenticatedSession?> ExecuteAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        var session = await sessionRepository.GetSessionByTokenHash(secretHasher.Hash(sessionToken), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var idleTimeout = options.Value.SessionIdleTimeout;

        if (session is null || !session.IsActive(now, idleTimeout))
        {
            return null;
        }

        var user = await userRepository.GetUserById(session.UserId, cancellationToken);

        if (user is null || !user.CanSignIn || user.SecurityStamp != session.SecurityStamp)
        {
            session.Revoke(now);
            await sessionRepository.SaveChangesAsync(cancellationToken);

            logger.SessionInvalidated(session.Id, session.UserId);

            return null;
        }

        // The saved activity lags behind by up to the interval, which shortens the effective idle timeout. Scaling the
        // interval to a tenth of the timeout keeps the loss at 10%, so short timeouts do not sign out active Users.
        var activityUpdateInterval = idleTimeout / 10 < MaxActivityUpdateInterval ? idleTimeout / 10 : MaxActivityUpdateInterval;

        if (now - session.LastActivityAt >= activityUpdateInterval)
        {
            session.RecordActivity(now);
            await sessionRepository.SaveChangesAsync(cancellationToken);
        }

        return new AuthenticatedSession(session.Id, user.Id, user.Email, user.FirstName, user.LastName, session.CreatedAt);
    }
}
