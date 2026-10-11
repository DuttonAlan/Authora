// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Roles.GetAll;

/// <summary>
///     Represents a single Role in the response for getting all Roles.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
/// <param name="Name" example="support">Unique technical Name of the Role.</param>
/// <param name="DisplayName" example="Support">Human-readable Name of the Role.</param>
/// <param name="Description" example="Can view Users and reset their lockout.">Description of the Role, or null if it has none.</param>
/// <param name="IsSystem" example="false">Whether the Role is a built-in System Role, which cannot be renamed or deleted.</param>
public sealed record GetAllRolesResponse(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystem);
