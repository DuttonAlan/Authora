// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Roles.Get;

/// <summary>
///     Represents the response for getting a Role.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
/// <param name="Name" example="support">Unique technical Name of the Role.</param>
/// <param name="DisplayName" example="Support">Human-readable Name of the Role.</param>
/// <param name="Description" example="Can view Users and reset their lockout.">Description of the Role, or null if it has none.</param>
/// <param name="IsSystem" example="false">Whether the Role is a built-in System Role, which cannot be renamed or deleted.</param>
/// <param name="CreatedAt">Point in time (UTC) when the Role was created.</param>
/// <param name="ModifiedAt">Point in time (UTC) when the Role was last modified, or null if it was never modified.</param>
/// <param name="Permissions">Permissions granted to the Role.</param>
public sealed record GetRoleResponse(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<RolePermissionResponse> Permissions);
