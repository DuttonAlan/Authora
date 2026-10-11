// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Permissions.GetAll;

/// <summary>
///     Represents a single Permission in the response for getting all Permissions.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-3fa85f64afa6">Id of the Permission.</param>
/// <param name="Name" example="users.create">Unique technical Name of the Permission.</param>
public sealed record GetAllPermissionsResponse(Guid Id, string Name);
