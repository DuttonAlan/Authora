// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Users;

/// <summary>
///     Prevents changes that would leave Authora without an administrator who can sign in.
/// </summary>
internal static class AdministratorGuard
{
    /// <summary>
    ///     Checks whether the Role is the built-in administrator Role.
    /// </summary>
    /// <param name="role">The Role to check.</param>
    /// <returns>True if the Role is the System Role <see cref="Role.AdministratorName"/>.</returns>
    public static bool IsAdministratorRole(Role role)
    {
        return role.IsSystem && role.Name == Role.AdministratorName;
    }

    /// <summary>
    ///     Throws if the User is the last administrator who can sign in.
    /// </summary>
    /// <remarks>
    ///     The Roles of the User must be loaded.
    /// </remarks>
    /// <param name="userRepository">The repository to count the other administrators.</param>
    /// <param name="user">The User that is about to lose administrator access.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <exception cref="ConflictException">Thrown when the User is the last administrator who can sign in.</exception>
    public static async Task EnsureNotLastAdministratorAsync(
        IUserRepository userRepository,
        User user,
        CancellationToken cancellationToken)
    {
        // A User who cannot sign in does not count as an administrator anyway.
        if (user.State is not (UserState.Active or UserState.Inactive))
        {
            return;
        }

        var administratorRole = user.Roles.FirstOrDefault(IsAdministratorRole);

        if (administratorRole is null)
        {
            return;
        }

        var otherAdministrators = await userRepository.CountUsersWithRoleWhoCanSignIn(
            administratorRole.Id,
            user.Id,
            cancellationToken);

        if (otherAdministrators == 0)
        {
            throw new ConflictException("The User is the last administrator and cannot lose administrator access.");
        }
    }
}
