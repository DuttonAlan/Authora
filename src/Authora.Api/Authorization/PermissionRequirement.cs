// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Requires the User of the Access Token to have a Permission through one of their Roles.
/// </summary>
/// <param name="permissionName">The Name of the Permission, see <see cref="Core.Application.Authorization.PermissionNames"/>.</param>
public sealed class PermissionRequirement(string permissionName) : IAuthorizationRequirement
{
    /// <summary>
    ///     The Name of the required Permission.
    /// </summary>
    public string PermissionName { get; } = permissionName;
}
