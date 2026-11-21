// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.VerifyEmail;

/// <summary>
///     The result of <see cref="VerifyEmailService"/>.
/// </summary>
public enum VerifyEmailResult
{
    /// <summary>
    ///     The Email of the User is verified now.
    /// </summary>
    Verified = 0,

    /// <summary>
    ///     The link does not work: it is unknown, expired, already used, replaced by a newer link, or its User can no
    ///     longer sign in or has another address now.
    /// </summary>
    InvalidLink = 1
}
