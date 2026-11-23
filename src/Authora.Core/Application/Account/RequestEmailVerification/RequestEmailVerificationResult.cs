// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.RequestEmailVerification;

/// <summary>
///     The result of <see cref="RequestEmailVerificationService"/>.
/// </summary>
public enum RequestEmailVerificationResult
{
    /// <summary>
    ///     A new link was sent; earlier links of the User no longer work.
    /// </summary>
    Sent = 0,

    /// <summary>
    ///     The Email of the User is already verified, so no link was sent.
    /// </summary>
    AlreadyVerified = 1,

    /// <summary>
    ///     The User already got <see cref="EmailVerificationOptions.MaxEmailsPerHour"/> links in the last hour, so no link
    ///     was sent.
    /// </summary>
    TooManyLinks = 2,

    /// <summary>
    ///     The mail server could not be reached or rejected the email. The new link was not delivered.
    /// </summary>
    DeliveryFailed = 3,

    /// <summary>
    ///     The User no longer exists or can no longer sign in.
    /// </summary>
    CannotSignIn = 4
}
