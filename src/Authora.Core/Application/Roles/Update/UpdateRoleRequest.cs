// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Roles.Update;

/// <summary>
///     Represents the request to update a Role.
/// </summary>
/// <param name="Name" example="support">New technical Name of the Role, or null to leave it unchanged. Must be unique. System Roles cannot be renamed.</param>
/// <param name="DisplayName" example="Support">New human-readable Name of the Role, or null to leave it unchanged.</param>
/// <param name="Description" example="Can view Users and reset their lockout.">New Description of the Role, null to leave it unchanged, or an empty string to remove it.</param>
public sealed record UpdateRoleRequest(
    [StringLength(Role.NameMaxLength, MinimumLength = 1), RegularExpression(Role.NamePattern)] string? Name,
    [StringLength(Role.DisplayNameMaxLength, MinimumLength = 1)] string? DisplayName,
    [StringLength(Role.DescriptionMaxLength)] string? Description);
