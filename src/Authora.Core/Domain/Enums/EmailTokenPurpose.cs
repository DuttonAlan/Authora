// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     What an <see cref="Entities.EmailToken"/> may be used for. A token is only accepted for its own purpose.
/// </summary>
public enum EmailTokenPurpose
{
    /// <summary>
    ///     Sets a new password for a User who forgot theirs.
    /// </summary>
    PasswordReset = 0,

    /// <summary>
    ///     Confirms that the User receives email at their address.
    /// </summary>
    EmailVerification = 1
}
