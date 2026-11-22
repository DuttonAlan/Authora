// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa.StartEnrollment;

/// <summary>
///     What the User adds to their authenticator app.
/// </summary>
/// <param name="Secret">The secret in Base32, in groups of four characters, to type in by hand.</param>
/// <param name="Uri">The <c>otpauth://</c> URI with the secret, shown as a QR code.</param>
/// <param name="ExpiresAt">Point in time (UTC) until which the setup can be confirmed.</param>
public sealed record TotpEnrollmentResponse(string Secret, string Uri, DateTime ExpiresAt)
{
    /// <summary>
    ///     Returns a description without the secret, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the response.</returns>
    public override string ToString() => $"TotpEnrollmentResponse(ExpiresAt = {ExpiresAt:O})";
}
