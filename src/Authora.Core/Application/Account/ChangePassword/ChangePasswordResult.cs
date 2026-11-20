// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.ChangePassword;

/// <summary>
///     The result of <see cref="ChangePasswordService"/>.
/// </summary>
public enum ChangePasswordResult
{
    /// <summary>
    ///     The password was changed. The current Session stays valid, all other Sessions and all tokens were revoked.
    /// </summary>
    Changed = 0,

    /// <summary>
    ///     The current password was wrong. Counted as a failed sign-in attempt for the lockout.
    /// </summary>
    WrongPassword = 1,

    /// <summary>
    ///     The User is locked out after too many wrong passwords and has to wait until the lockout ends.
    /// </summary>
    LockedOut = 2,

    /// <summary>
    ///     The Session is no longer valid, e.g. because it was revoked or the User can no longer sign in.
    /// </summary>
    SessionEnded = 3
}
