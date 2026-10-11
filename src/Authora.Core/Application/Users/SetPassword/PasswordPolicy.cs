// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.SetPassword;

/// <summary>
///     The rules a new password must follow. Length is the only rule, as recommended by NIST SP 800-63B:
///     composition rules (e.g. "one digit") do not make passwords stronger.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    ///     Minimum length of a password.
    /// </summary>
    public const int MinLength = 12;

    /// <summary>
    ///     Maximum length of a password, which limits the work of hashing very long inputs.
    /// </summary>
    public const int MaxLength = 128;
}
