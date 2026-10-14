// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Groups.Create;

/// <summary>
///     Represents the request to create a Group.
/// </summary>
/// <param name="Name" example="support-team">Unique technical Name of the Group. Only lowercase letters, digits, '.', '_' and '-'.</param>
/// <param name="DisplayName" example="Support Team">Human-readable Name of the Group.</param>
/// <param name="Description" example="Everyone who answers customer requests.">Optional Description of the Group.</param>
public sealed record CreateGroupRequest(
    [Required, StringLength(Group.NameMaxLength, MinimumLength = 1), RegularExpression(Group.NamePattern)] string Name,
    [Required, StringLength(Group.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName,
    [StringLength(Group.DescriptionMaxLength)] string? Description);
