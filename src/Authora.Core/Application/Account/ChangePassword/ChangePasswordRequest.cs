// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.ChangePassword;

/// <summary>
///     Represents the request of a signed-in User to change their own password.
/// </summary>
/// <param name="CurrentPassword">The current password, which proves that the User, not just their browser, asks.</param>
/// <param name="NewPassword">The new password, following the same policy as passwords set by an administrator.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword)
{
    /// <summary>
    ///     Returns a description without the passwords, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => "ChangePasswordRequest(***)";
}
