// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.PendingSignIn;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Passkeys.StartAuthentication;

/// <summary>
///     The Service to start the passkey step of a pending sign-in: creates the options with a new challenge for
///     <c>navigator.credentials.get</c>, allowing only the passkeys of the User.
/// </summary>
/// <remarks>
///     Not recorded in the audit log: a challenge changes nothing until a response to it is verified, see
///     <see cref="PasskeyVerifier"/>.
/// </remarks>
/// <param name="reader">Loads and checks the pending sign-in.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to load the passkeys of the User.</param>
/// <param name="challengeIssuer">Creates the challenge.</param>
/// <param name="relyingParty">Creates the options.</param>
public sealed class StartPasskeyAuthenticationService(
    PendingSignInReader reader,
    IPasskeyRepository passkeyRepository,
    PasskeyChallengeIssuer challengeIssuer,
    IPasskeyRelyingParty relyingParty)
{
    /// <summary>
    ///     Creates the options for the pending sign-in of the browser.
    /// </summary>
    /// <param name="challengeToken">The plain-text token of the pending sign-in from its cookie.</param>
    /// <param name="currentSessionId">Id of the Session of the browser, or null if it has none.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>
    ///     The <c>PublicKeyCredentialRequestOptions</c> as JSON, or null if the pending sign-in cannot be completed (anymore)
    ///     or not with a passkey.
    /// </returns>
    public async Task<string?> ExecuteAsync(string? challengeToken, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var pending = await reader.ReadAsync(challengeToken, currentSessionId, cancellationToken);

        if (pending is null || !pending.Factors.OfType<PasskeySecondFactor>().Any())
        {
            return null;
        }

        var passkeys = await passkeyRepository.GetPasskeys(pending.User.Id, cancellationToken);

        if (passkeys.Count == 0)
        {
            return null;
        }

        var challenge = await challengeIssuer.IssueAsync(pending.User.Id, PasskeyCeremony.Authentication, cancellationToken);

        return relyingParty.CreateAuthenticationOptions(
            challenge,
            [.. passkeys.Select(passkey => new PasskeyDescriptor(passkey.CredentialId, passkey.TransportList))]);
    }
}
