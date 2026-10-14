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
    ///     Gets the effective Roles of a User, ordered by their Name: the Roles assigned directly and the Roles of the
    ///     Groups of the User, together with where each Role comes from.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Roles of the User.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<EffectiveRoleResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserWithPermissionsById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        return EffectiveRoles.Resolve(user)
            .Select(x => x.Response)
            .ToList();
    }
}
