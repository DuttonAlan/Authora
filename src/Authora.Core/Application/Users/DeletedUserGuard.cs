// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Users;

/// <summary>
///     Prevents changes to deleted Users, which are only kept for history.
/// </summary>
internal static class DeletedUserGuard
{
    /// <summary>
    ///     Throws if the User is deleted.
    /// </summary>
    /// <param name="user">The User that is about to be changed.</param>
    /// <exception cref="ConflictException">Thrown when the User is <see cref="UserState.Deleted"/>.</exception>
    public static void EnsureNotDeleted(User user)
    {
        if (user.State == UserState.Deleted)
        {
            throw new ConflictException("A deleted User cannot be changed.");
        }
    }
}
