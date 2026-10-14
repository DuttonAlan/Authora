// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users;

/// <summary>
///     Represents a Role a User has, and where it comes from.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
/// <param name="Name" example="support">Unique technical Name of the Role.</param>
/// <param name="DisplayName" example="Support">Human-readable Name of the Role.</param>
/// <param name="IsDirect" example="false">Whether the Role is assigned to the User directly.</param>
/// <param name="Groups">The Groups of the User that have the Role, ordered by their Name. Empty if the User only has the Role directly.</param>
public sealed record EffectiveRoleResponse(
    Guid Id,
    string Name,
    string DisplayName,
    bool IsDirect,
    IReadOnlyList<UserGroupResponse> Groups);
