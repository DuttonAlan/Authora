// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.ResetPassword;

/// <summary>
///     Represents the request to set a new password with the token of a reset link.
/// </summary>
/// <param name="Token">The one-time token from the link.</param>
/// <param name="NewPassword">The new password, following the same policy as passwords set by an administrator.</param>
public sealed record ResetPasswordRequest(string Token, string NewPassword)
{
    /// <summary>
    ///     Returns a description without the token and the password, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => "ResetPasswordRequest(***)";
}
