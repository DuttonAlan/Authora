// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Roles.Create;

/// <summary>
///     Represents the request to create a Role.
/// </summary>
/// <param name="Name" example="support">Unique technical Name of the Role. Only lowercase letters, digits, '.', '_' and '-'.</param>
/// <param name="DisplayName" example="Support">Human-readable Name of the Role.</param>
/// <param name="Description" example="Can view Users and reset their lockout.">Optional Description of what the Role is used for.</param>
public sealed record CreateRoleRequest(
    [Required, StringLength(Role.NameMaxLength, MinimumLength = 1), RegularExpression(Role.NamePattern)] string Name,
    [Required, StringLength(Role.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName,
    [StringLength(Role.DescriptionMaxLength)] string? Description);
