// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Roles.Create;

/// <summary>
///     Represents the response for creating a Role.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the created Role.</param>
public sealed record CreateRoleResponse(Guid Id);
