// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Mfa.RegenerateRecoveryCodes;

/// <summary>
///     The Service for a signed-in User to replace their recovery codes with a new set, e.g. after using most of them
///     or when the old ones may have been seen. The previous codes, used or not, stop working.
/// </summary>
/// <remarks>
///     New codes sign in like the authenticator app, so only a Session in which the User completed it (<c>otp</c>)
///     may generate them. Otherwise a Session that was signed in with the password alone, e.g. before the app was set
///     up, could get codes and use one to remove the app without ever having it. Such a Session first completes a
///     step-up.
/// </remarks>
/// <param name="sessionRepository">An Instance of a ISessionRepository to check how the Session was authenticated.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check that the Session is still valid.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to replace the codes.</param>
/// <param name="secretHasher">The hasher of the codes.</param>
/// <param name="timeProvider">The TimeProvider for the creation time and the Session checks.</param>
/// <param name="options">The sign-in settings with the idle timeout.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class RegenerateRecoveryCodesService(
    ISessionRepository sessionRepository,
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<RegenerateRecoveryCodesService> logger)
{
    /// <summary>
    ///     Replaces the recovery codes of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User generates the codes in.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The outcome, with the new codes in plain text to show the User once.</returns>
    /// <exception cref="ConflictException">Thrown when another request changed the codes at the same time.</exception>
    public async Task<RegenerateRecoveryCodesResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
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
            return RegenerateRecoveryCodesResult.SessionEnded;
        }

        // Held until the commit, so a removal or a reset cannot delete the authenticator between the check and saving
        // codes that would then stand in for nothing.
        await using var transaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        if (!await mfaRepository.HasConfirmedTotpAuthenticator(user.Id, cancellationToken))
        {
            return RegenerateRecoveryCodesResult.NotEnrolled;
        }

        if (!session.AuthenticationMethods.Contains(AuthenticationMethods.OneTimePassword, StringComparer.Ordinal))
        {
            logger.RecoveryCodesNeedStepUp(user.Id, session.Id);

            return RegenerateRecoveryCodesResult.StepUpRequired;
        }

        var codes = await RecoveryCodeIssuer.ReplaceAsync(mfaRepository, secretHasher, user.Id, now, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Mfa.RecoveryCodesRegenerated,
            user.Id,
            $"{codes.Count} recovery codes, Session {session.Id}"));

        await mfaRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.RecoveryCodesRegenerated(user.Id);

        return new RegenerateRecoveryCodesResult(RegenerateRecoveryCodesStatus.Regenerated, codes);
    }
}
