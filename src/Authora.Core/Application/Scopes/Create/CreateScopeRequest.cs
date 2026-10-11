// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Scopes.Create;

/// <summary>
///     Represents the request to create a Scope.
/// </summary>
/// <param name="Name" example="orders.read">Unique, case-sensitive Name of the Scope as Clients send it in the scope parameter. Printable ASCII without spaces, '"' and '\' (RFC 6749, Section 3.3). Cannot be changed later.</param>
/// <param name="DisplayName" example="Read your orders">Human-readable Name of the Scope, shown on the consent screen.</param>
/// <param name="Description" example="Allows the application to view your past and current orders.">Optional Description of the access the Scope grants, shown on the consent screen.</param>
public sealed record CreateScopeRequest(
    [Required, StringLength(Scope.NameMaxLength, MinimumLength = 1), RegularExpression(Scope.NamePattern)] string Name,
    [Required, StringLength(Scope.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName,
    [StringLength(Scope.DescriptionMaxLength)] string? Description);
