// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Roles.Get;

/// <summary>
///     The Service to get a Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to get a Role.</param>
public sealed class GetRoleService(IRoleRepository roleRepository)
{
    /// <summary>
    ///     Gets a Role based on the Id, including its Permissions.
    /// </summary>
    /// <param name="id">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Role, or null if the Role does not exist.</returns>
    public async Task<GetRoleResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetRoleById(id, cancellationToken);

        if (role is null)
        {
            return null;
        }

        var permissions = role.Permissions
            .OrderBy(permission => permission.Name, StringComparer.Ordinal)
            .Select(permission => new RolePermissionResponse(permission.Id, permission.Name))
            .ToList();

        return new GetRoleResponse(
            role.Id,
            role.Name,
            role.DisplayName,
            role.Description,
            role.IsSystem,
            role.CreatedAt,
            role.ModifiedAt,
            permissions);
    }
}
