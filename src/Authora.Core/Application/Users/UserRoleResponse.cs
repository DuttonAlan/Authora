// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users;

/// <summary>
///     Represents a Role assigned to a User.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
/// <param name="Name" example="admin">Unique technical Name of the Role.</param>
/// <param name="DisplayName" example="Administrator">Human-readable Name of the Role.</param>
public sealed record UserRoleResponse(Guid Id, string Name, string DisplayName);
