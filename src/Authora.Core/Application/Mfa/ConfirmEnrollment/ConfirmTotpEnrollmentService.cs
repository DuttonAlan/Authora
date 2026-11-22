// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Mfa;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Mfa.ConfirmEnrollment;

/// <summary>
///     The Service for a signed-in User to finish setting up an authenticator app with a first code, which proves that
///     the app has the secret. From then on the User enters a code after the password, and gets a first set of
///     recovery codes.
/// </summary>
/// <remarks>
///     A wrong code does not count towards the lockout: the User is signed in, and the secret is theirs anyway. The
///     time step of the confirming code cannot be used again to sign in.
/// </remarks>
/// <param name="mfaRepository">An Instance of a IMfaRepository to confirm the authenticator and store the recovery codes.</param>
/// <param name="secretProtector">Decrypts the secret.</param>
/// <param name="secretHasher">The hasher of the recovery codes.</param>
/// <param name="timeProvider">The TimeProvider for the current time step.</param>
/// <param name="auditLog">The audit log to record the setup.</param>
/// <param name="logger">The Logger to record the setup.</param>
public sealed class ConfirmTotpEnrollmentService(
    IMfaRepository mfaRepository,
    ITotpSecretProtector secretProtector,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<ConfirmTotpEnrollmentService> logger)
{
    /// <summary>
    ///     Checks the first code and confirms the setup.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="code">The code the app shows.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The outcome, with the recovery codes once confirmed.</returns>
    /// <exception cref="ConflictException">Thrown when another request confirmed or restarted the setup at the same time.</exception>
    public async Task<ConfirmTotpEnrollmentResult> ExecuteAsync(
        Guid userId,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var authenticator = await mfaRepository.GetTotpAuthenticator(userId, cancellationToken);

        if (authenticator is { IsConfirmed: true })
        {
            return ConfirmTotpEnrollmentResult.AlreadyEnrolled;
        }

        if (authenticator is null || !authenticator.CanBeConfirmed(now.UtcDateTime))
        {
            return ConfirmTotpEnrollmentResult.Expired;
        }

        var secret = secretProtector.Unprotect(authenticator.ProtectedSecret);

        if (secret is null)
        {
            logger.TotpSecretUnreadable(userId);

            return ConfirmTotpEnrollmentResult.Expired;
        }

        long? timeStep;

        try
        {
            var compact = string.Concat((code ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
            timeStep = Totp.FindTimeStep(secret, compact, now);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        if (timeStep is null)
        {
            return ConfirmTotpEnrollmentResult.WrongCode;
        }

        authenticator.Confirm(timeStep.Value, now.UtcDateTime);

        var recoveryCodes = await RecoveryCodeIssuer.ReplaceAsync(mfaRepository, secretHasher, userId, now.UtcDateTime, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Mfa.Enrolled,
            userId,
            $"Authenticator app, {recoveryCodes.Count} recovery codes"));

        // Throws a ConflictException if another request confirmed or restarted it since it was loaded.
        await mfaRepository.SaveChangesAsync(cancellationToken);

        logger.TotpEnrolled(userId, recoveryCodes.Count);

        return new ConfirmTotpEnrollmentResult(ConfirmTotpEnrollmentStatus.Confirmed, recoveryCodes);
    }
}
