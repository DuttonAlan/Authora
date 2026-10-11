// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Checks whether a User may still use codes and tokens that were issued earlier.
/// </summary>
internal static class TokenUserGuard
{
    /// <summary>
    ///     Checks whether the User still exists, can sign in, and has the Security Stamp the code or token was
    ///     issued with. A different Security Stamp means e.g. "sign out everywhere", a new password or deactivation.
    /// </summary>
    /// <param name="user">The User, or null if the User does not exist.</param>
    /// <param name="securityStamp">The Security Stamp stored with the code or token.</param>
    /// <returns>True if the User may still use the code or token.</returns>
    public static bool CanUse([NotNullWhen(true)] User? user, Guid securityStamp)
    {
        return user is not null && user.CanSignIn && user.SecurityStamp == securityStamp;
    }
}
