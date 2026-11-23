// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     Checks the response of a passkey to the challenge of a sign-in (WebAuthn assertion).
/// </summary>
/// <remarks>
///     <para>
///         The challenge is consumed first, in a single atomic update, so a response is accepted at most once and only
///         for a challenge that was issued for this User less than <see cref="Domain.Entities.PasskeyChallenge.Lifetime"/>
///         ago. The passkey must belong to the User; its signature is verified with the stored public key.
///     </para>
///     <para>
///         A signature counter that did not increase is rejected, since it may come from a cloned authenticator; the
///         passkey itself stays registered, so the User decides whether to remove it. The new counter is saved in an
///         atomic update that fails if another response with the same or a higher counter was saved meanwhile.
///     </para>
///     <para>
///         Records <see cref="AuditEventTypes.Passkeys.Used"/> or <see cref="AuditEventTypes.Passkeys.Failed"/>
///         immediately, because the challenge is used up whether or not the request completes. It does not count
///         failures towards the lockout; its callers do.
///     </para>
/// </remarks>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to load the passkey and record its use.</param>
/// <param name="challengeIssuer">Consumes the challenge.</param>
/// <param name="relyingParty">Verifies the response.</param>
/// <param name="timeProvider">The TimeProvider for the time of use.</param>
/// <param name="auditLog">The audit log to record the outcome.</param>
/// <param name="logger">The Logger to record the outcome.</param>
public sealed class PasskeyVerifier(
    IPasskeyRepository passkeyRepository,
    PasskeyChallengeIssuer challengeIssuer,
    IPasskeyRelyingParty relyingParty,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<PasskeyVerifier> logger)
{
    /// <summary>
    ///     Checks the response of a passkey and records its use if it is valid.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="responseJson">The response of <c>navigator.credentials.get</c> as JSON.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The method the Session records (<c>hwk</c> or <c>swk</c>), or null if the response is not valid.</returns>
    public async Task<string?> VerifyAsync(Guid userId, string? responseJson, CancellationToken cancellationToken = default)
    {
        var response = relyingParty.ReadResponse(responseJson);

        if (response is null)
        {
            return await RejectAsync(userId, "Malformed response", cancellationToken);
        }

        if (!await challengeIssuer.TryConsumeAsync(response.Challenge, userId, PasskeyCeremony.Authentication, cancellationToken))
        {
            return await RejectAsync(userId, "Unknown, used or expired challenge", cancellationToken);
        }

        var passkey = await passkeyRepository.FindPasskeyByCredentialId(userId, response.CredentialId, cancellationToken);

        if (passkey is null)
        {
            return await RejectAsync(userId, "Not a passkey of the User", cancellationToken);
        }

        var verification = await relyingParty.VerifyAuthenticationAsync(
            responseJson!,
            new PasskeyAuthenticationRequest(
                response.Challenge,
                PasskeyChallengeIssuer.UserHandle(userId),
                passkey.CredentialId,
                passkey.PublicKey,
                (uint)passkey.SignCount),
            cancellationToken);

        // Also checked here, whatever the library does: a counter that did not increase may come from a clone.
        var signCountDidNotIncrease = verification.SignCountDidNotIncrease
            || (verification.Succeeded
                && (verification.SignCount != 0 || passkey.SignCount != 0)
                && verification.SignCount <= passkey.SignCount);

        if (signCountDidNotIncrease)
        {
            logger.PasskeySignCountDidNotIncrease(userId, passkey.Id, passkey.SignCount, verification.SignCount);

            return await RejectAsync(
                userId,
                $"Passkey {passkey.Id}: the signature counter did not increase, the authenticator may have been cloned",
                cancellationToken);
        }

        if (!verification.Succeeded)
        {
            return await RejectAsync(userId, $"Passkey {passkey.Id}: {verification.FailureReason}", cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Atomic: of two responses with the same counter, only one gets past this.
        if (!await passkeyRepository.TryRecordUse(passkey.Id, verification.SignCount, verification.IsBackedUp, now, cancellationToken))
        {
            return await RejectAsync(
                userId,
                $"Passkey {passkey.Id}: removed, or another response with the same signature counter was accepted",
                cancellationToken);
        }

        var method = passkey.AuthenticationMethod;

        logger.PasskeyVerified(userId, passkey.Id);
        await auditLog.RecordNowAsync(
            AuditEntry.ForUser(AuditEventTypes.Passkeys.Used, userId, $"Passkey {passkey.Id} ({method})"),
            cancellationToken);

        return method;
    }

    private async Task<string?> RejectAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        logger.PasskeyRejected(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Passkeys.Failed, userId, reason);
        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);

        return null;
    }
}
