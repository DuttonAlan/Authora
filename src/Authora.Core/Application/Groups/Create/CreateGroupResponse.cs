// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Groups.Create;

/// <summary>
///     Represents the response for creating a Group.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the created Group.</param>
public sealed record CreateGroupResponse(Guid Id);
