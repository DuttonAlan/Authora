// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Scopes.Update;

/// <summary>
///     Represents the request to update a Scope. The Name of a Scope cannot be changed.
/// </summary>
/// <param name="DisplayName" example="Read your orders">New human-readable Name of the Scope, or null to leave it unchanged.</param>
/// <param name="Description" example="Allows the application to view your past and current orders.">New Description of the Scope, null to leave it unchanged, or an empty string to remove it.</param>
public sealed record UpdateScopeRequest(
    [StringLength(Scope.DisplayNameMaxLength, MinimumLength = 1)] string? DisplayName,
    [StringLength(Scope.DescriptionMaxLength)] string? Description);
