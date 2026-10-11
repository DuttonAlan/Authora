// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Roles;

/// <summary>
///     Represents a Permission granted to a Role.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-3fa85f64afa6">Id of the Permission.</param>
/// <param name="Name" example="users.create">Unique technical Name of the Permission.</param>
public sealed record RolePermissionResponse(Guid Id, string Name);
