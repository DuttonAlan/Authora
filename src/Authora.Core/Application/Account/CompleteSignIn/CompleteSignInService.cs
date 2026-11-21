// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.PendingSignIn;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.CompleteSignIn;

/// <summary>
///     The Service to complete a pending sign-in with a second factor: starts the Session, or, for a step-up, adds
///     the factor to the existing Session and gives it a new token.
/// </summary>
/// <remarks>
///     <para>
///         A wrong response counts towards the lockout like a wrong password; the failed attempts are only reset once
///         the sign-in is complete. The pending sign-in stays usable after a wrong response until it expires or the
///         User is locked out.
///     </para>
///     <para>
///         A pending sign-in is completed at most once: it is marked as completed in a single atomic update after the
///         response was verified, so of two concurrent requests only one gets a Session.
///     </para>
/// </remarks>
/// <param name="reader">Loads and checks the pending sign-in.</param>
/// <param name="challengeRepository">An Instance of a ISignInChallengeRepository to complete the pending sign-in.</param>
/// <param name="userRepository">An Instance of a IUserRepository to count failures and save the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to create the Session.</param>
/// <param name="secretGenerator">The generator for the Session token.</param>
/// <param name="secretHasher">The hasher for the stored Session token.</param>
/// <param name="timeProvider">The TimeProvider for the lockout and the Session lifetime.</param>
/// <param name="options">The sign-in settings.</param>
/// <param name="auditLog">The audit log to record sign-ins, step-ups and failures.</param>
/// <param name="logger">The Logger to record sign-ins and failures.</param>
public sealed class CompleteSignInService(
    PendingSignInReader reader,
    ISignInChallengeRepository challengeRepository,
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<CompleteSignInService> logger)
{
    /// <summary>
    ///     Checks the second factor and completes the pending sign-in.
    /// </summary>
    /// <param name="request">The token of the pending sign-in, the chosen factor and the response.</param>
    /// <param name="currentSessionId">Id of the Session of the browser, or null. A step-up must be completed in its Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The outcome, with the Session token if the sign-in or step-up is complete.</returns>
    public async Task<CompleteSignInResult> ExecuteAsync(
        CompleteSignInRequest request,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pending = await reader.ReadAsync(request.ChallengeToken, currentSessionId, cancellationToken);

        if (pending is null)
        {
            return CompleteSignInResult.Expired;
        }

        var (challenge, user, session, factors) = pending;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var eventType = session is null ? AuditEventTypes.Account.SignIn : AuditEventTypes.Account.StepUp;

        if (user.IsLockedOut(now))
        {
            logger.SignInFailed(user.Id, "the User is locked out");
            await RecordFailureAsync(eventType, user.Id, $"The User is locked out, pending sign-in {challenge.Id}", cancellationToken);

            return CompleteSignInResult.LockedOut;
        }

        // A factor that is unknown, not enrolled or already completed, e.g. from a manipulated form.
        var factor = factors.FirstOrDefault(factor => string.Equals(factor.Method, request.Method, StringComparison.Ordinal));

        if (factor is null)
        {
            logger.PendingSignInRejected(challenge.Id, "the chosen second factor cannot be completed");

            return CompleteSignInResult.Expired;
        }

        if (string.IsNullOrEmpty(request.Response) || !await factor.VerifyAsync(user.Id, request.Response, cancellationToken))
        {
            return await RecordWrongResponseAsync(eventType, challenge, user.Id, factor.Method, now, cancellationToken);
        }

        if (!await challengeRepository.TryConsume(challenge.Id, now, cancellationToken))
        {
            logger.PendingSignInRejected(challenge.Id, "another request completed it");

            return CompleteSignInResult.Expired;
        }

        try
        {
            return session is null
                ? await StartSessionAsync(challenge, user, factor.Method, now, cancellationToken)
                : await StepUpAsync(session, user, factor.Method, now, cancellationToken);
        }
        catch (ConflictException)
        {
            // The User was changed since it was loaded, e.g. by a password reset. The pending sign-in is used up.
            logger.PendingSignInRejected(challenge.Id, "the User was changed by another request at the same time");

            return CompleteSignInResult.Expired;
        }
    }

    private async Task<CompleteSignInResult> StartSessionAsync(
        SignInChallenge challenge,
        User user,
        string method,
        DateTime now,
        CancellationToken cancellationToken)
    {
        user.RecordSuccessfulSignIn(now);

        // Throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        var sessionToken = secretGenerator.Generate();
        var session = Session.Create(
            user,
            secretHasher.Hash(sessionToken),
            [.. challenge.AuthenticationMethods, method],
            now,
            options.Value.SessionLifetime);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.SignIn,
            user.Id,
            $"Session {session.Id} ({session.AuthenticationMethod}), pending sign-in {challenge.Id}"));

        await sessionRepository.CreateSession(session, cancellationToken);

        logger.SignedIn(user.Id, session.Id);

        return new CompleteSignInResult(CompleteSignInStatus.SignedIn, sessionToken);
    }

    private async Task<CompleteSignInResult> StepUpAsync(
        Session session,
        User user,
        string method,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sessionToken = secretGenerator.Generate();

        session.RecordStepUp(secretHasher.Hash(sessionToken), method, now);
        user.RecordSuccessfulSignIn(now);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.StepUp,
            user.Id,
            $"Session {session.Id} ({session.AuthenticationMethod})"));

        // User and Session share the request's DbContext, so both are saved together with the audit event. Throws a
        // ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        logger.SteppedUp(user.Id, session.Id, method);

        return new CompleteSignInResult(CompleteSignInStatus.SteppedUp, sessionToken);
    }

    private async Task<CompleteSignInResult> RecordWrongResponseAsync(
        string eventType,
        SignInChallenge challenge,
        Guid userId,
        string method,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Counted like a wrong password, atomically in the database.
        var lockoutEndAt = await userRepository.RecordFailedSignIn(
            userId,
            now,
            options.Value.MaxFailedAttempts,
            options.Value.LockoutDuration,
            cancellationToken);

        logger.SecondFactorFailed(userId, challenge.Id, method);
        await RecordFailureAsync(eventType, userId, $"Wrong second factor ({method}), pending sign-in {challenge.Id}", cancellationToken);

        if (lockoutEndAt > now)
        {
            logger.LockedOut(userId, lockoutEndAt.Value);

            await auditLog.RecordNowAsync(
                AuditEntry.ForUser(AuditEventTypes.Account.LockedOut, userId, $"Until {lockoutEndAt.Value:O}"),
                cancellationToken);

            return CompleteSignInResult.LockedOut;
        }

        return CompleteSignInResult.WrongResponse;
    }

    private Task RecordFailureAsync(string eventType, Guid userId, string reason, CancellationToken cancellationToken)
    {
        var entry = AuditEntry.ForUser(eventType, userId, reason);

        return auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }
}
