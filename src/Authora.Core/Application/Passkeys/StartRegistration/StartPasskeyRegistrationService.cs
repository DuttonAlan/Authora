// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Passkeys.StartRegistration;

/// <summary>
///     The Service for a signed-in User to start registering a passkey: creates the options with a new challenge for
///     <c>navigator.credentials.create</c>, which <see cref="Register.RegisterPasskeyService"/> then verifies.
/// </summary>
/// <remarks>
///     The existing passkeys of the User are excluded, so an authenticator is not registered twice. Not recorded in the
///     audit log: a challenge changes nothing until the registration is verified.
/// </remarks>
/// <param name="guard">Checks the Session.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to load the passkeys of the User.</param>
/// <param name="challengeIssuer">Creates the challenge.</param>
/// <param name="relyingParty">Creates the options.</param>
public sealed class StartPasskeyRegistrationService(
    PasskeyChangeGuard guard,
    IPasskeyRepository passkeyRepository,
    PasskeyChallengeIssuer challengeIssuer,
    IPasskeyRelyingParty relyingParty)
{
    /// <summary>
    ///     Creates the options to register a passkey.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session of the browser.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The options, or why the User cannot register a passkey now.</returns>
    public async Task<StartPasskeyRegistrationResult> ExecuteAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var (user, _, needsStepUp) = await guard.CheckAsync(userId, sessionId, cancellationToken);

        if (user is null)
        {
            return new StartPasskeyRegistrationResult(StartPasskeyRegistrationStatus.SessionEnded);
        }

        if (needsStepUp)
        {
            return new StartPasskeyRegistrationResult(StartPasskeyRegistrationStatus.StepUpRequired);
        }

        var passkeys = await passkeyRepository.GetPasskeys(user.Id, cancellationToken);

        if (passkeys.Count >= Passkey.MaxPerUser)
        {
            return new StartPasskeyRegistrationResult(StartPasskeyRegistrationStatus.LimitReached);
        }

        var challenge = await challengeIssuer.IssueAsync(user.Id, PasskeyCeremony.Registration, cancellationToken);
        var options = relyingParty.CreateRegistrationOptions(RegistrationRequest.For(user, challenge, passkeys));

        return new StartPasskeyRegistrationResult(StartPasskeyRegistrationStatus.Started, options);
    }
}

/// <summary>
///     The outcome of <see cref="StartPasskeyRegistrationService"/>.
/// </summary>
public enum StartPasskeyRegistrationStatus
{
    /// <summary>
    ///     The options were created.
    /// </summary>
    Started,

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

/// <summary>
///     The result of <see cref="StartPasskeyRegistrationService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="OptionsJson">The <c>PublicKeyCredentialCreationOptions</c> as JSON, or null unless it was started.</param>
public sealed record StartPasskeyRegistrationResult(StartPasskeyRegistrationStatus Status, string? OptionsJson = null);

/// <summary>
///     Builds the <see cref="PasskeyRegistrationRequest"/> of a User, the same for the options and their verification.
/// </summary>
internal static class RegistrationRequest
{
    public static PasskeyRegistrationRequest For(User user, byte[] challenge, IEnumerable<Passkey> existing)
    {
        var displayName = $"{user.FirstName} {user.LastName}".Trim();

        return new PasskeyRegistrationRequest(
            challenge,
            PasskeyChallengeIssuer.UserHandle(user.Id),
            user.Email,
            displayName.Length == 0 ? user.Email : displayName,
            [.. existing.Select(passkey => new PasskeyDescriptor(passkey.CredentialId, passkey.TransportList))]);
    }
}
