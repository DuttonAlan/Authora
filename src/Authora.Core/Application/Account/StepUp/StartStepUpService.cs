// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.StepUp;

/// <summary>
///     The Service for a signed-in User to start a step-up: completing an additional factor for the current Session,
///     e.g. because an application asks for a stronger sign-in.
/// </summary>
/// <remarks>
///     Starts a pending sign-in bound to the Session, which <see cref="CompleteSignIn.CompleteSignInService"/>
///     completes. Until then, the Session stays as it is.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to load the Session.</param>
/// <param name="challengeRepository">An Instance of a ISignInChallengeRepository to create the pending sign-in.</param>
/// <param name="secondFactors">The second factors, to find one the Session lacks.</param>
/// <param name="secretGenerator">The generator for the token of the pending sign-in.</param>
/// <param name="secretHasher">The hasher for the stored token.</param>
/// <param name="timeProvider">The TimeProvider for the Session checks and the expiry.</param>
/// <param name="options">The sign-in settings.</param>
/// <param name="auditLog">The audit log to record the start.</param>
/// <param name="logger">The Logger to record the start.</param>
public sealed class StartStepUpService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    ISignInChallengeRepository challengeRepository,
    SecondFactorRegistry secondFactors,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<StartStepUpService> logger)
{
    /// <summary>
    ///     Starts a step-up of the Session, if the User enrolled a factor the Session lacks.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session of the browser.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>
    ///     The plain-text token of the pending sign-in for its cookie, or null if the Session ended or already has
    ///     every factor the User enrolled.
    /// </returns>
    public async Task<string?> ExecuteAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await userRepository.GetUserById(userId, cancellationToken);
        var session = await sessionRepository.GetSessionById(sessionId, cancellationToken);

        // Checked again, although the page only runs with a valid Session: it may have ended since.
        if (user is null
            || !user.CanSignIn
            || session is null
            || session.UserId != user.Id
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, options.Value.SessionIdleTimeout))
        {
            return null;
        }

        var completed = session.AuthenticationMethods;
        var missing = (await secondFactors.GetEnrolledAsync(user.Id, cancellationToken))
            .Any(factor => !completed.Contains(factor.Method, StringComparer.Ordinal));

        if (!missing)
        {
            return null;
        }

        var challengeToken = secretGenerator.Generate();
        var challenge = SignInChallenge.Create(
            secretHasher.Hash(challengeToken),
            user,
            completed,
            now,
            options.Value.PendingSignInLifetime,
            session);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.SignInPending,
            user.Id,
            $"Pending sign-in {challenge.Id}, step-up of Session {session.Id}"));

        await challengeRepository.CreateChallenge(challenge, cancellationToken);

        logger.StepUpStarted(user.Id, challenge.Id, session.Id);

        return challengeToken;
    }
}
