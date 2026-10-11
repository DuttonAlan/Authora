// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.SignIn;

/// <summary>
///     The Service to sign a User in with Email and password and start a new Session.
/// </summary>
/// <remarks>
///     Every failure returns the same result, whether the Email is unknown, the password is wrong, the User has no
///     password, cannot sign in or is locked out (US-007). For unknown Users, a password is still verified, so the
///     response time does not reveal whether the Email exists.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to create the Session.</param>
/// <param name="passwordHasher">The hasher to verify the password.</param>
/// <param name="secretGenerator">The generator for the Session token.</param>
/// <param name="secretHasher">The hasher for the stored Session token.</param>
/// <param name="timeProvider">The TimeProvider for the lockout and the Session lifetime.</param>
/// <param name="options">The sign-in settings.</param>
/// <param name="logger">The Logger to record sign-ins.</param>
public sealed class SignInService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordHasher passwordHasher,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    ILogger<SignInService> logger)
{
    // How often a sign-in is tried when the User is changed by another request at the same time.
    private const int MaxAttempts = 3;

    // Hash of a random password, verified for Users that cannot sign in, so they take as long as real ones.
    private static string? dummyPasswordHash;

    /// <summary>
    ///     Checks the credentials and starts a new Session.
    /// </summary>
    /// <param name="email">The Email the User entered.</param>
    /// <param name="password">The password the User entered.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The plain-text token of the new Session, or null if the sign-in failed.</returns>
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

            return SignInResult.Failed;
        }

        if (user.IsLockedOut(now))
        {
            VerifyDummyPassword(password);
            logger.SignInFailed(user.Id, "the User is locked out");

            return SignInResult.Failed;
        }

        if (!user.CanSignIn)
        {
            VerifyDummyPassword(password);
            logger.SignInFailed(user.Id, "the User cannot sign in");

            return SignInResult.Failed;
        }

        var verification = passwordHasher.Verify(user.PasswordHash!, password);

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

            if (lockoutEndAt > now)
            {
                logger.LockedOut(user.Id, lockoutEndAt.Value);
            }

            return SignInResult.Failed;
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.RehashPassword(passwordHasher.Hash(password));
        }

        user.RecordSuccessfulSignIn(now);

        // Throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        var sessionToken = secretGenerator.Generate();
        var session = Session.Create(user, secretHasher.Hash(sessionToken), now, options.Value.SessionLifetime);

        await sessionRepository.CreateSession(session, cancellationToken);

        logger.SignedIn(user.Id, session.Id);

        return new SignInResult(sessionToken);
    }

    private void VerifyDummyPassword(string password)
    {
        dummyPasswordHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
        _ = passwordHasher.Verify(dummyPasswordHash, password);
    }
}

/// <summary>
///     The result of <see cref="SignInService"/>.
/// </summary>
/// <param name="SessionToken">The plain-text token of the new Session for the cookie, or null if the sign-in failed.</param>
public sealed record SignInResult(string? SessionToken)
{
    /// <summary>
    ///     The result of a failed sign-in.
    /// </summary>
    public static readonly SignInResult Failed = new(SessionToken: null);

    /// <summary>
    ///     Whether the sign-in succeeded.
    /// </summary>
    public bool Succeeded => SessionToken is not null;

    /// <summary>
    ///     Returns a description without the Session token, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the result.</returns>
    public override string ToString() => $"SignInResult(Succeeded = {Succeeded})";
}
