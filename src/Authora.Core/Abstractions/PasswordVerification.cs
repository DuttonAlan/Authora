// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     The result of <see cref="IPasswordHasher.Verify(string, string)"/>.
/// </summary>
public enum PasswordVerification
{
    /// <summary>
    ///     The password does not match.
    /// </summary>
    Failed = 0,

    /// <summary>
    ///     The password matches.
    /// </summary>
    Success = 1,

    /// <summary>
    ///     The password matches, but the hash uses outdated parameters and should be replaced by a new hash.
    /// </summary>
    SuccessRehashNeeded = 2
}
