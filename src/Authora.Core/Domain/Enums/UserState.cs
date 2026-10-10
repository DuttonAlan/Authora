// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Defines the State of a User.
/// </summary>
public enum UserState
{
    /// <summary>
    ///     The User is active and can sign in.
    /// </summary>
    Active = 0,

    /// <summary>
    ///     The User did not sign in for a defined period of time. The User can still sign in,
    ///     which makes the User <see cref="Active"/> again.
    /// </summary>
    Inactive = 1,

    /// <summary>
    ///     The User was deactivated by an administrator and cannot sign in. The data of the User is kept.
    /// </summary>
    Deactivated = 2,

    /// <summary>
    ///     The User was deleted and cannot sign in. The record is only kept for history.
    /// </summary>
    Deleted = 3
}
