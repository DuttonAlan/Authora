// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

using Authora.Core.Domain.Exceptions;

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

    /// <summary>
    ///     Throws when a password does not follow the policy.
    /// </summary>
    /// <param name="password">The password to check.</param>
    /// <param name="paramName">The name of the parameter. Filled in by the compiler.</param>
    /// <exception cref="DomainValidationException">Thrown when the password is missing, too short or too long.</exception>
    public static void ThrowIfInvalid(
        string? password,
        [CallerArgumentExpression(nameof(password))] string? paramName = null)
    {
        if (password is not { Length: >= MinLength and <= MaxLength })
        {
            throw new DomainValidationException(
                $"The password must be {MinLength} to {MaxLength} characters long.",
                paramName);
        }
    }
}
