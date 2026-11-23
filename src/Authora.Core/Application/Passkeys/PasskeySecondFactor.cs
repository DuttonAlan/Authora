// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Domain;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     Passkeys as a second factor of the sign-in. The response is the JSON of <c>navigator.credentials.get</c>; the
///     Session records <c>hwk</c> for a device-bound passkey and <c>swk</c> for one that may be synced, both of which
///     reach <c>phr</c> (phishing-resistant).
/// </summary>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to check whether the User registered a passkey.</param>
/// <param name="verifier">Checks the response.</param>
public sealed class PasskeySecondFactor(IPasskeyRepository passkeyRepository, PasskeyVerifier verifier) : ISecondFactor
{
    /// <summary>
    ///     The name of the factor shown to Users.
    /// </summary>
    public const string Name = "Passkey";

    /// <inheritdoc/>
    public string Method => AuthenticationMethods.HardwareKey;

    /// <inheritdoc/>
    public IReadOnlyList<string> Methods { get; } = [AuthenticationMethods.HardwareKey, AuthenticationMethods.SoftwareKey];

    /// <inheritdoc/>
    public string DisplayName => Name;

    /// <inheritdoc/>
    public Task<bool> IsEnrolledAsync(Guid userId, CancellationToken cancellationToken = default)
        => passkeyRepository.HasPasskeys(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<string?> VerifyAsync(Guid userId, string response, CancellationToken cancellationToken = default)
        => verifier.VerifyAsync(userId, response, cancellationToken);
}
