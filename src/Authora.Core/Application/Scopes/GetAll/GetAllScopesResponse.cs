// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Scopes.GetAll;

/// <summary>
///     Represents a single Scope in the response for getting all Scopes.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
/// <param name="Name" example="orders.read">Unique Name of the Scope as used in the scope parameter.</param>
/// <param name="DisplayName" example="Read your orders">Human-readable Name of the Scope, shown on the consent screen.</param>
/// <param name="Description" example="Allows the application to view your past and current orders.">Description of the Scope, or null if it has none.</param>
/// <param name="IsSystem" example="false">Whether the Scope is a built-in System Scope, which cannot be deleted.</param>
public sealed record GetAllScopesResponse(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystem);
