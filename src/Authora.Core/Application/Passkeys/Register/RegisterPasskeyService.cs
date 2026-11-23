// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Passkeys.StartRegistration;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys.Register;

/// <summary>
///     The Service for a signed-in User to finish registering a passkey: verifies the response of
///     <c>navigator.credentials.create</c> and stores the new passkey under the name the User chose.
/// </summary>
/// <remarks>
///     The challenge of <see cref="StartPasskeyRegistrationService"/> is consumed first, so a response is accepted at
///     most once. No attestation is requested: Authora trusts the authenticator the User chose and records its AAGUID
///     only for display. From now on the User can use the passkey after the password.
/// </remarks>
/// <param name="guard">Checks the Session.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to serialize changes of the User's second factors.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to store the passkey.</param>
/// <param name="challengeIssuer">Consumes the challenge.</param>
/// <param name="relyingParty">Verifies the response.</param>
/// <param name="timeProvider">The TimeProvider for the creation time.</param>
/// <param name="auditLog">The audit log to record the registration.</param>
/// <param name="logger">The Logger to record the registration.</param>
public sealed class RegisterPasskeyService(
    PasskeyChangeGuard guard,
    IMfaRepository mfaRepository,
    IPasskeyRepository passkeyRepository,
    PasskeyChallengeIssuer challengeIssuer,
    IPasskeyRelyingParty relyingParty,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<RegisterPasskeyService> logger)
{
    /// <summary>
    ///     Verifies the response and stores the passkey.
    /// </summary>
    /// <param name="request">The User, the Session, the name and the response.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether it was registered, or why not.</returns>
    /// <exception cref="DomainValidationException">Thrown when the name is empty, too long or contains control characters.</exception>
    /// <exception cref="Exceptions.ConflictException">Thrown when another request registered the same passkey at the same time.</exception>
    public async Task<RegisterPasskeyResult> ExecuteAsync(RegisterPasskeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked before the challenge is consumed, so the User can correct the name.
        var name = Passkey.NormalizeName(request.Name);

        var (user, _, needsStepUp) = await guard.CheckAsync(request.UserId, request.SessionId, cancellationToken);

        if (user is null)
        {
            return RegisterPasskeyResult.SessionEnded;
        }

        if (needsStepUp)
        {
            return RegisterPasskeyResult.StepUpRequired;
        }

        var response = relyingParty.ReadResponse(request.ResponseJson);

        if (response is null)
        {
            return await RejectAsync(user.Id, "Malformed response", cancellationToken);
        }

        if (!await challengeIssuer.TryConsumeAsync(response.Challenge, user.Id, PasskeyCeremony.Registration, cancellationToken))
        {
            return await RejectAsync(user.Id, "Unknown, used or expired challenge", cancellationToken);
        }

        var existing = await passkeyRepository.GetPasskeys(user.Id, cancellationToken);

        var verification = await relyingParty.VerifyRegistrationAsync(
            request.ResponseJson!,
            RegistrationRequest.For(user, response.Challenge, existing),
            cancellationToken);

        if (verification.Credential is not { } credential)
        {
            return await RejectAsync(user.Id, verification.FailureReason ?? "Invalid response", cancellationToken);
        }

        // Held until the commit, so concurrent registrations cannot exceed the limit, and a reset cannot miss the new
        // passkey.
        await using var transaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        if ((await passkeyRepository.GetPasskeys(user.Id, cancellationToken)).Count >= Passkey.MaxPerUser)
        {
            logger.PasskeyRegistrationFailed(user.Id, "the User has too many passkeys");

            return RegisterPasskeyResult.LimitReached;
        }

        // Unique in the database as well; this check gives the User a clear message.
        if (await passkeyRepository.CredentialIdExists(credential.CredentialId, cancellationToken))
        {
            return await RejectAsync(user.Id, "The passkey is already registered", cancellationToken, RegisterPasskeyResult.AlreadyRegistered);
        }

        var passkey = Passkey.Create(
            user.Id,
            name,
            credential.CredentialId,
            credential.PublicKey,
            credential.SignCount,
            credential.AaGuid,
            credential.Transports,
            credential.IsBackupEligible,
            credential.IsBackedUp,
            timeProvider.GetUtcNow().UtcDateTime);

        passkeyRepository.AddPasskey(passkey);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Passkeys.Registered,
            user.Id,
            $"Passkey {passkey.Id} ({passkey.AuthenticationMethod}), AAGUID {passkey.AaGuid}"));

        await passkeyRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasskeyRegistered(user.Id, passkey.Id);

        return RegisterPasskeyResult.Registered;
    }

    private async Task<RegisterPasskeyResult> RejectAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken,
        RegisterPasskeyResult result = RegisterPasskeyResult.Rejected)
    {
        logger.PasskeyRegistrationFailed(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Passkeys.Registered, userId, reason);
        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);

        return result;
    }
}

/// <summary>
///     A passkey a signed-in User registers.
/// </summary>
/// <param name="UserId">Id of the signed-in User.</param>
/// <param name="SessionId">Id of the Session of the browser.</param>
/// <param name="Name">The name the User gives it, e.g. "Phone".</param>
/// <param name="ResponseJson">The response of <c>navigator.credentials.create</c> as JSON.</param>
public sealed record RegisterPasskeyRequest(Guid UserId, Guid SessionId, string? Name, string? ResponseJson)
{
    /// <summary>
    ///     Returns a description without the response.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"RegisterPasskeyRequest(UserId = {UserId}, SessionId = {SessionId})";
}

/// <summary>
///     The outcome of <see cref="RegisterPasskeyService"/>.
/// </summary>
public enum RegisterPasskeyResult
{
    /// <summary>
    ///     The passkey is registered.
    /// </summary>
    Registered,

    /// <summary>
    ///     The response was not valid, e.g. its challenge expired or was used, or its origin is wrong.
    /// </summary>
    Rejected,

    /// <summary>
    ///     The passkey is already registered.
    /// </summary>
    AlreadyRegistered,

    /// <summary>
    ///     The User has <see cref="Passkey.MaxPerUser"/> passkeys and must remove one first.
    /// </summary>
    LimitReached,

    /// <summary>
    ///     The User has a second factor but did not complete one in the Session, see <see cref="PasskeyChangeGuard"/>.
    /// </summary>
    StepUpRequired,

    /// <summary>
    ///     The Session ended in the meantime; the User must sign in again.
    /// </summary>
    SessionEnded
}
