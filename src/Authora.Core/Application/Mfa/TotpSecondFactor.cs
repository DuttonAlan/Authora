// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Domain;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     The authenticator app as a second factor of the sign-in. A recovery code is accepted instead of a code of the
///     app; either way the Session records <c>otp</c>.
/// </summary>
/// <param name="mfaRepository">An Instance of a IMfaRepository to check whether the User set up an authenticator app.</param>
/// <param name="verifier">Checks the code.</param>
public sealed class TotpSecondFactor(IMfaRepository mfaRepository, MfaVerifier verifier) : ISecondFactor
{
    /// <summary>
    ///     The name of the factor shown to Users.
    /// </summary>
    public const string Name = "Authenticator app";

    /// <inheritdoc/>
    public string Method => AuthenticationMethods.OneTimeCode;

    /// <inheritdoc/>
    public string DisplayName => Name;

    /// <inheritdoc/>
    public Task<bool> IsEnrolledAsync(Guid userId, CancellationToken cancellationToken = default)
        => mfaRepository.HasConfirmedTotpAuthenticator(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> VerifyAsync(Guid userId, string response, CancellationToken cancellationToken = default)
        => verifier.VerifyAsync(userId, response, cancellationToken);
}
