// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Groups;

/// <summary>
///     Represents a Role assigned to a Group.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
/// <param name="Name" example="support">Unique technical Name of the Role.</param>
/// <param name="DisplayName" example="Support">Human-readable Name of the Role.</param>
public sealed record GroupRoleResponse(Guid Id, string Name, string DisplayName);
