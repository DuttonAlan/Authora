// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.ResetPassword;

/// <summary>
///     The result of <see cref="ResetPasswordService"/>.
/// </summary>
public enum ResetPasswordResult
{
    /// <summary>
    ///     The password was set. All Sessions and tokens of the User were revoked and a lockout ended.
    /// </summary>
    Reset = 0,

    /// <summary>
    ///     The link does not work: it is unknown, expired, already used, replaced by a newer link, or its User can no
    ///     longer sign in or has another address now.
    /// </summary>
    InvalidLink = 1
}
