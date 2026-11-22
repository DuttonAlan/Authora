// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.SignIn;

/// <summary>
///     The Service to sign a User in with Email and password and start a new Session, or a pending sign-in if the User
///     enrolled a second factor.
/// </summary>
/// <remarks>
///     <para>
///         Every failure returns the same result, whether the Email is unknown, the password is wrong, the User has no
///         password, cannot sign in or is locked out. For unknown Users, a password is still verified, so the
///         response time does not reveal whether the Email exists.
///     </para>
///     <para>
///         A User with a second factor gets no Session for the password alone, only a short-lived pending sign-in,
///         which <see cref="CompleteSignIn.CompleteSignInService"/> turns into a Session.
///     </para>
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to create the Session.</param>
/// <param name="challengeRepository">An Instance of a ISignInChallengeRepository to create a pending sign-in.</param>
/// <param name="secondFactors">The second factors, to check whether the User must complete one.</param>
/// <param name="passwordHasher">The hasher to verify the password.</param>
/// <param name="secretGenerator">The generator for the Session token and the token of a pending sign-in.</param>
/// <param name="secretHasher">The hasher for the stored tokens.</param>
/// <param name="timeProvider">The TimeProvider for the lockout and the Session lifetime.</param>
/// <param name="options">The sign-in settings.</param>
/// <param name="administratorMfaPolicy">Decides whether a User without a second factor must set one up right away.</param>
/// <param name="auditLog">The audit log to record sign-ins and lockouts.</param>
/// <param name="logger">The Logger to record sign-ins.</param>
public sealed class SignInService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    ISignInChallengeRepository challengeRepository,
    SecondFactorRegistry secondFactors,
    IPasswordHasher passwordHasher,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    AdministratorMfaPolicy administratorMfaPolicy,
    IAuditLog auditLog,
    ILogger<SignInService> logger)
{
    // How often a sign-in is tried when the User is changed by another request at the same time.
    private const int MaxAttempts = 3;

    // Hash of a random password, verified for Users that cannot sign in, so they take as long as real ones.
    private static string? dummyPasswordHash;

    /// <summary>
    ///     Checks the credentials and starts a new Session or a pending sign-in.
    /// </summary>
    /// <param name="email">The Email the User entered.</param>
    /// <param name="password">The password the User entered.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The token of the new Session or of the pending sign-in, or <see cref="SignInResult.Failed"/>.</returns>
    public async Task<SignInResult> ExecuteAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        // If another request changes the User while the password is verified (e.g. a password reset or a lockout),
        // saving fails and the sign-in starts over with the current User, so it never overwrites that change.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TrySignInAsync(email, password, cancellationToken);
            }
            catch (ConflictException) when (attempt < MaxAttempts)
            {
                logger.SignInRetried(attempt);
            }
            catch (ConflictException)
            {
                logger.SignInRetried(attempt);

                return SignInResult.Failed;
            }
        }
    }

    private async Task<SignInResult> TrySignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetUserByNormalizedEmail(User.Normalize(email), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (user is null)
        {
            VerifyDummyPassword(password);
            logger.SignInFailedUnknownUser();

            // The Email is not recorded: it may be a password typed into the wrong field.
            await RecordFailureAsync(userId: null, "Unknown Email", cancellationToken);

            return SignInResult.Failed;
        }

        if (user.IsLockedOut(now))
        {
            VerifyDummyPassword(password);
            logger.SignInFailed(user.Id, "the User is locked out");
            await RecordFailureAsync(user.Id, "The User is locked out", cancellationToken);

            return SignInResult.Failed;
        }

        if (!user.CanSignIn)
        {
            VerifyDummyPassword(password);
            logger.SignInFailed(user.Id, "the User cannot sign in");
            await RecordFailureAsync(user.Id, "The User cannot sign in", cancellationToken);

            return SignInResult.Failed;
        }

        var verification = passwordHasher.Verify(user.PasswordHash, password);

        if (verification == PasswordVerification.Failed)
        {
            // Counted atomically in the database, so concurrent wrong passwords cannot count as one.
            var lockoutEndAt = await userRepository.RecordFailedSignIn(
                user.Id,
                now,
                options.Value.MaxFailedAttempts,
                options.Value.LockoutDuration,
                cancellationToken);

            logger.SignInFailed(user.Id, "wrong password");
            await RecordFailureAsync(user.Id, "Wrong password", cancellationToken);

            if (lockoutEndAt > now)
            {
                logger.LockedOut(user.Id, lockoutEndAt.Value);

                await auditLog.RecordNowAsync(
                    AuditEntry.ForUser(AuditEventTypes.Account.LockedOut, user.Id, $"Until {lockoutEndAt.Value:O}"),
                    cancellationToken);
            }

            return SignInResult.Failed;
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.RehashPassword(passwordHasher.Hash(password));
        }

        if ((await secondFactors.GetEnrolledAsync(user.Id, cancellationToken)).Count > 0)
        {
            return await StartPendingSignInAsync(user, now, cancellationToken);
        }

        user.RecordSuccessfulSignIn(now);

        // Throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        var sessionToken = secretGenerator.Generate();
        var session = Session.Create(user, secretHasher.Hash(sessionToken), [AuthenticationMethods.Password], now, options.Value.SessionLifetime);

        // Recorded after the User was saved: a retry after a conflict must not record the sign-in twice.
        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.SignIn, user.Id, $"Session {session.Id} ({session.AuthenticationMethod})"));

        await sessionRepository.CreateSession(session, cancellationToken);

        logger.SignedIn(user.Id, session.Id);

        // The User has no second factor (otherwise the sign-in would be pending), but must use one: they set it up
        // right away, with this Session, so nobody is locked out by turning the requirement on.
        var mustEnroll = await administratorMfaPolicy.IsRequiredAsync(user.Id, cancellationToken);

        if (mustEnroll)
        {
            logger.SecondFactorEnrollmentRequired(user.Id);
        }

        return SignInResult.SignedIn(sessionToken, mustEnroll);
    }

    // The password is correct, but the User enrolled a second factor: no Session yet, only a pending sign-in. The
    // failed attempts are not reset, so knowing the password does not allow guessing the second factor endlessly;
    // they are reset once the second factor is completed, see CompleteSignInService.
    private async Task<SignInResult> StartPendingSignInAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        // Saves a rehashed password; throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        var challengeToken = secretGenerator.Generate();
        var challenge = SignInChallenge.Create(
            secretHasher.Hash(challengeToken),
            user,
            [AuthenticationMethods.Password],
            now,
            options.Value.PendingSignInLifetime);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.SignInPending, user.Id, $"Pending sign-in {challenge.Id}"));

        await challengeRepository.CreateChallenge(challenge, cancellationToken);

        logger.SignInPending(user.Id, challenge.Id);

        return SignInResult.Pending(challengeToken);
    }

    private async Task RecordFailureAsync(Guid? userId, string reason, CancellationToken cancellationToken)
    {
        var entry = userId is null
            ? new AuditEntry(AuditEventTypes.Account.SignIn, Details: reason)
            : AuditEntry.ForUser(AuditEventTypes.Account.SignIn, userId.Value, reason);

        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }

    private void VerifyDummyPassword(string password)
    {
        dummyPasswordHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
        _ = passwordHasher.Verify(dummyPasswordHash, password);
    }
}

/// <summary>
///     The result of <see cref="SignInService"/>: a new Session, a pending sign-in that waits for a second factor, or a
///     failure.
/// </summary>
public sealed record SignInResult
{
    /// <summary>
    ///     The result of a failed sign-in.
    /// </summary>
    public static readonly SignInResult Failed = new();

    private SignInResult()
    {
    }

    /// <summary>
    ///     The plain-text token of the new Session for the Session cookie, or null.
    /// </summary>
    public string? SessionToken { get; private init; }

    /// <summary>
    ///     The plain-text token of the pending sign-in for its own cookie, or null.
    /// </summary>
    public string? ChallengeToken { get; private init; }

    /// <summary>
    ///     Whether the User is signed in, but must set up a second factor right away, because they hold a Permission and
    ///     multi-factor authentication is required for administrators.
    /// </summary>
    public bool MustEnrollSecondFactor { get; private init; }

    /// <summary>
    ///     Whether the User is signed in with a new Session.
    /// </summary>
    public bool Succeeded => SessionToken is not null;

    /// <summary>
    ///     Whether the password was correct and the User must complete a second factor.
    /// </summary>
    public bool IsPending => ChallengeToken is not null;

    /// <summary>
    ///     Creates the result of a completed sign-in.
    /// </summary>
    /// <param name="sessionToken">The plain-text token of the new Session.</param>
    /// <param name="mustEnrollSecondFactor">Whether the User must set up a second factor right away.</param>
    /// <returns>The result.</returns>
    public static SignInResult SignedIn(string sessionToken, bool mustEnrollSecondFactor = false) => new()
    {
        SessionToken = sessionToken,
        MustEnrollSecondFactor = mustEnrollSecondFactor
    };

    /// <summary>
    ///     Creates the result of a sign-in that waits for a second factor.
    /// </summary>
    /// <param name="challengeToken">The plain-text token of the pending sign-in.</param>
    /// <returns>The result.</returns>
    public static SignInResult Pending(string challengeToken) => new() { ChallengeToken = challengeToken };

    /// <summary>
    ///     Returns a description without the tokens, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"SignInResult(Succeeded = {Succeeded}, IsPending = {IsPending}, MustEnrollSecondFactor = {MustEnrollSecondFactor})";
}
