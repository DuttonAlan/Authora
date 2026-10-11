// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Users.GetRoles;

/// <summary>
///     The Service to get the Roles of a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get the User.</param>
public sealed class GetUserRolesService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets the Roles assigned to a User, ordered by their Name.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Roles of the User.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<UserRoleResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        return user.Roles
            .OrderBy(role => role.Name, StringComparer.Ordinal)
            .Select(role => new UserRoleResponse(role.Id, role.Name, role.DisplayName))
            .ToList();
    }
}
