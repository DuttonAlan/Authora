// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Groups.Get;

/// <summary>
///     Represents the response for getting a Group.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
/// <param name="Name" example="support-team">Unique technical Name of the Group.</param>
/// <param name="DisplayName" example="Support Team">Human-readable Name of the Group.</param>
/// <param name="Description" example="Everyone who answers customer requests.">Description of the Group, or null if it has none.</param>
/// <param name="CreatedAt">Point in time (UTC) when the Group was created.</param>
/// <param name="ModifiedAt">Point in time (UTC) when the Group was last modified, or null if it was never modified.</param>
/// <param name="Roles">Roles every member of the Group has, ordered by their Name.</param>
public sealed record GetGroupResponse(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<GroupRoleResponse> Roles);
