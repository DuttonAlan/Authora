// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Mfa;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Checks a code a User entered for their second factor: a code of their authenticator app, or one of their
///     recovery codes.
/// </summary>
/// <remarks>
///     <para>
///         Six digits are checked as a code of the authenticator app (TOTP, RFC 6238), anything else as a recovery
///         code. An accepted code is used up at once in a single atomic update: a code of the app cannot be used again,
///         nor any code of an earlier time step, and a recovery code only once.
///     </para>
///     <para>
///         Records <see cref="AuditEventTypes.Mfa.Verified"/> or <see cref="AuditEventTypes.Mfa.RecoveryCodeUsed"/>
///         immediately, because the code is used up whether or not the request completes. It does not count failures
///         towards the lockout; its callers do.
///     </para>
/// </remarks>
/// <param name="mfaRepository">An Instance of a IMfaRepository to load the authenticator and use the codes.</param>
/// <param name="secretProtector">Decrypts the secret of the authenticator.</param>
/// <param name="secretHasher">The hasher of the recovery codes.</param>
/// <param name="timeProvider">The TimeProvider for the current time step.</param>
/// <param name="auditLog">The audit log to record the outcome.</param>
/// <param name="logger">The Logger to record the outcome.</param>
public sealed class MfaVerifier(
    IMfaRepository mfaRepository,
    ITotpSecretProtector secretProtector,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<MfaVerifier> logger)
{
    /// <summary>
    ///     Checks a code of the authenticator app or a recovery code, and uses it up if it is correct.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="response">The code the User entered. Spaces do not matter, nor case and dashes in a recovery code.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the code is correct and was not used before.</returns>
    public async Task<bool> VerifyAsync(Guid userId, string? response, CancellationToken cancellationToken = default)
    {
        var input = response?.Trim() ?? string.Empty;
        var compact = string.Concat(input.Where(character => !char.IsWhiteSpace(character)));

        if (Totp.IsCodeFormat(compact))
        {
            return await VerifyTotpAsync(userId, compact, cancellationToken);
        }

        if (RecoveryCodeFormat.TryNormalize(input, out var recoveryCode))
        {
            return await VerifyRecoveryCodeAsync(userId, recoveryCode, cancellationToken);
        }

        return await RejectAsync(userId, "Neither a code of the authenticator app nor a recovery code", cancellationToken);
    }

    private async Task<bool> VerifyTotpAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var authenticator = await mfaRepository.FindConfirmedTotpAuthenticator(userId, cancellationToken);

        if (authenticator is null)
        {
            return await RejectAsync(userId, "No authenticator app is set up", cancellationToken);
        }

        var secret = secretProtector.Unprotect(authenticator.ProtectedSecret);

        if (secret is null)
        {
            logger.TotpSecretUnreadable(userId);

            return await RejectAsync(userId, "The secret of the authenticator app cannot be decrypted", cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        long? timeStep;

        try
        {
            timeStep = Totp.FindTimeStep(secret, code, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (timeStep is null)
        {
            return await RejectAsync(userId, "Wrong code of the authenticator app", cancellationToken);
        }

        // Atomic: of two requests with the same code, only one gets past this.
        if (!await mfaRepository.TryRecordTotpUse(authenticator.Id, timeStep.Value, now.UtcDateTime, cancellationToken))
        {
            return await RejectAsync(userId, "The code of the authenticator app was already used", cancellationToken);
        }

        logger.TotpVerified(userId);
        await auditLog.RecordNowAsync(
            AuditEntry.ForUser(AuditEventTypes.Mfa.Verified, userId, "Code of the authenticator app"),
            cancellationToken);

        return true;
    }

    private async Task<bool> VerifyRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        // Recovery codes only stand in for an authenticator app; they are removed with it, this is a second check.
        if (!await mfaRepository.HasConfirmedTotpAuthenticator(userId, cancellationToken))
        {
            return await RejectAsync(userId, "No authenticator app is set up", cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!await mfaRepository.TryUseRecoveryCode(userId, secretHasher.Hash(code), now, cancellationToken))
        {
            return await RejectAsync(userId, "Wrong or already used recovery code", cancellationToken);
        }

        var remaining = await mfaRepository.CountUnusedRecoveryCodes(userId, cancellationToken);

        logger.RecoveryCodeUsed(userId, remaining);
        await auditLog.RecordNowAsync(
            AuditEntry.ForUser(AuditEventTypes.Mfa.RecoveryCodeUsed, userId, $"{remaining} recovery codes left"),
            cancellationToken);

        return true;
    }

    private async Task<bool> RejectAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        logger.VerificationFailed(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Mfa.Verified, userId, reason);
        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);

        return false;
    }
}
