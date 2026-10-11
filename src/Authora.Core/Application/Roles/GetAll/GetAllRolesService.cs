// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Roles.GetAll;

/// <summary>
///     The Service to get all Roles.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to get all Roles.</param>
public sealed class GetAllRolesService(IRoleRepository roleRepository)
{
    /// <summary>
    ///     Gets all Roles ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Roles.</returns>
    public async Task<IReadOnlyList<GetAllRolesResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await roleRepository.GetRoles(cancellationToken);

        return roles
            .Select(role => new GetAllRolesResponse(
                role.Id,
                role.Name,
                role.DisplayName,
                role.Description,
                role.IsSystem))
            .ToList();
    }
}
