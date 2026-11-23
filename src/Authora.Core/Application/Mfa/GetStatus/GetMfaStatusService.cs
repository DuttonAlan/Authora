// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Passkeys;

namespace Authora.Core.Application.Mfa.GetStatus;

/// <summary>
///     The Service to get the multi-factor authentication of a User, for the User's own account page and for
///     administrators.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to check that the User exists.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to load the authenticator and the codes.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to load the passkeys.</param>
public sealed class GetMfaStatusService(IUserRepository userRepository, IMfaRepository mfaRepository, IPasskeyRepository passkeyRepository)
{
    /// <summary>
    ///     Gets the authenticator app, the recovery codes and the passkeys of a User.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The multi-factor authentication of the User.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<MfaStatusResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await userRepository.GetUserById(userId, cancellationToken) is null)
        {
            throw new NotFoundException($"User '{userId}' was not found.");
        }

        var authenticator = await mfaRepository.GetTotpAuthenticator(userId, cancellationToken);
        var codes = await mfaRepository.GetRecoveryCodes(userId, cancellationToken);

        var authenticatorApp = authenticator is { ConfirmedAt: { } enrolledAt }
            ? new AuthenticatorAppStatusResponse(enrolledAt, authenticator.LastUsedAt)
            : null;

        var recoveryCodes = codes.Count > 0
            ? new RecoveryCodesStatusResponse(
                codes.Count(code => code.UsedAt is null),
                codes.Count,
                codes.Min(code => code.CreatedAt),
                codes.Max(code => code.UsedAt))
            : null;

        var passkeys = (await passkeyRepository.GetPasskeys(userId, cancellationToken)).Select(PasskeyResponse.From).ToList();

        return new MfaStatusResponse(authenticatorApp is not null || passkeys.Count > 0, authenticatorApp, recoveryCodes, passkeys);
    }
}
