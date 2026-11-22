// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa.Remove;

/// <summary>
///     The outcome of <see cref="RemoveTotpService"/>.
/// </summary>
public enum RemoveTotpResult
{
    /// <summary>
    ///     The authenticator app and the recovery codes are removed; all other Sessions of the User ended.
    /// </summary>
    Removed,

    /// <summary>
    ///     The code was wrong or already used.
    /// </summary>
    WrongCode,

    /// <summary>
    ///     The User is locked out after too many failed attempts.
    /// </summary>
    LockedOut,

    /// <summary>
    ///     The User has no authenticator app.
    /// </summary>
    NotEnrolled,

    /// <summary>
    ///     The Session ended in the meantime; the User must sign in again.
    /// </summary>
    SessionEnded
}
