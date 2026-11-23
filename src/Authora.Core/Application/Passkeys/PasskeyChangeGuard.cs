// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     Checks that a signed-in User may change their passkeys in their Session.
/// </summary>
/// <remarks>
///     A User who has a second factor must have completed one in the Session. Otherwise a Session that was signed in
///     with the password alone, e.g. before the second factor was set up, could add a passkey of its own or remove the
///     User's passkeys without ever having them. Such a Session first completes a step-up.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to load the Session.</param>
/// <param name="secondFactors">The second factors, to check whether the User enrolled one.</param>
/// <param name="timeProvider">The TimeProvider for the Session checks.</param>
/// <param name="options">The sign-in settings with the idle timeout.</param>
/// <param name="logger">The Logger to record a missing step-up.</param>
public sealed class PasskeyChangeGuard(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    SecondFactorRegistry secondFactors,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    ILogger<PasskeyChangeGuard> logger)
{
    /// <summary>
    ///     Loads the User and the Session and checks them.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session of the browser.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked User and Session, and whether the Session must step up first.</returns>
    public async Task<PasskeyChangeContext> CheckAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken);
        var session = await sessionRepository.GetSessionById(sessionId, cancellationToken);

        return await EvaluateAsync(user, session, cancellationToken);
    }

    /// <summary>
    ///     Reloads the User and the Session of an earlier <see cref="CheckAsync"/> and checks them again.
    /// </summary>
    /// <remarks>
    ///     Call it after taking the MFA lock (<see cref="IMfaRepository.BeginMfaChangeAsync"/>) when the change does not
    ///     save the User: an MFA reset or a removal that committed in the meantime rotated the User's security stamp, so
    ///     the Session no longer counts, and the change must not be saved.
    /// </remarks>
    /// <param name="context">The result of <see cref="CheckAsync"/> with a User and a Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The User and Session with their current values, and whether the Session must step up first.</returns>
    public async Task<PasskeyChangeContext> CheckAgainAsync(PasskeyChangeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User is not { } user || context.Session is not { } session)
        {
            return PasskeyChangeContext.SessionEnded;
        }

        await userRepository.ReloadUser(user, cancellationToken);
        await sessionRepository.ReloadSession(session, cancellationToken);

        return await EvaluateAsync(user, session, cancellationToken);
    }

    private async Task<PasskeyChangeContext> EvaluateAsync(User? user, Session? session, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Checked again, although the pages only run with a valid Session: it may have ended since.
        if (user is null
            || !user.CanSignIn
            || session is null
            || session.UserId != user.Id
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, options.Value.SessionIdleTimeout))
        {
            return PasskeyChangeContext.SessionEnded;
        }

        var enrolled = await secondFactors.GetEnrolledAsync(user.Id, cancellationToken);
        var needsStepUp = enrolled.Count > 0 && !AuthenticationMethods.IsMultiFactor(session.AuthenticationMethods);

        if (needsStepUp)
        {
            logger.PasskeyChangeNeedsStepUp(user.Id, session.Id);
        }

        return new PasskeyChangeContext(user, session, needsStepUp);
    }
}

/// <summary>
///     The result of <see cref="PasskeyChangeGuard"/>.
/// </summary>
/// <param name="User">The tracked User, or null if the Session ended.</param>
/// <param name="Session">The tracked Session, or null if it ended.</param>
/// <param name="NeedsStepUp">Whether the User must complete a second factor in the Session first.</param>
public sealed record PasskeyChangeContext(User? User, Session? Session, bool NeedsStepUp)
{
    /// <summary>
    ///     The result when the Session ended in the meantime.
    /// </summary>
    public static readonly PasskeyChangeContext SessionEnded = new(null, null, false);
}
