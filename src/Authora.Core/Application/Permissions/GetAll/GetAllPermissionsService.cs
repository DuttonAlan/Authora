// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Permissions.GetAll;

/// <summary>
///     The Service to get all Permissions.
/// </summary>
/// <param name="permissionRepository">An Instance of a IPermissionRepository to get all Permissions.</param>
public sealed class GetAllPermissionsService(IPermissionRepository permissionRepository)
{
    /// <summary>
    ///     Gets all Permissions ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Permissions.</returns>
    public async Task<IReadOnlyList<GetAllPermissionsResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await permissionRepository.GetPermissions(cancellationToken);

        return permissions
            .Select(permission => new GetAllPermissionsResponse(permission.Id, permission.Name))
            .ToList();
    }
}
