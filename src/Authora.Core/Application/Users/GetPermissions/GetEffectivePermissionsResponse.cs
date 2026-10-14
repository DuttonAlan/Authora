// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.GetPermissions;

/// <summary>
///     Represents a Permission a User has through one or more of its Roles, assigned directly or through a Group.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-3fa85f64afa6">Id of the Permission.</param>
/// <param name="Name" example="users.create">Unique technical Name of the Permission.</param>
/// <param name="GrantedBy">The Roles of the User that grant this Permission, ordered by their Name, each with where the User has it from.</param>
public sealed record GetEffectivePermissionsResponse(
    Guid Id,
    string Name,
    IReadOnlyList<EffectiveRoleResponse> GrantedBy);
