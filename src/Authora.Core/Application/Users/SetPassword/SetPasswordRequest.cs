// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Authora.Core.Application.Users.SetPassword;

/// <summary>
///     Represents the request to set the password of a User.
/// </summary>
/// <param name="Password" example="correct horse battery staple">The new password. 12 to 128 characters.</param>
public sealed record SetPasswordRequest(
    [Required, StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength)] string Password)
{
    /// <summary>
    ///     Returns a description without the password, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => "SetPasswordRequest(***)";
}
