// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Groups.Update;

/// <summary>
///     Represents the request to update a Group.
/// </summary>
/// <param name="Name" example="support-team">New technical Name of the Group, or null to leave it unchanged. Must be unique. Only lowercase letters, digits, '.', '_' and '-', without leading or trailing whitespace.</param>
/// <param name="DisplayName" example="Support Team">New human-readable Name of the Group, or null to leave it unchanged.</param>
/// <param name="Description" example="Everyone who answers customer requests.">New Description of the Group, null to leave it unchanged, or an empty string to remove it.</param>
public sealed record UpdateGroupRequest(
    [StringLength(Group.NameMaxLength, MinimumLength = 1), RegularExpression(Group.NamePattern)] string? Name,
    [StringLength(Group.DisplayNameMaxLength, MinimumLength = 1)] string? DisplayName,
    [StringLength(Group.DescriptionMaxLength)] string? Description);
