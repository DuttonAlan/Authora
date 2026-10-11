// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users.Update;

/// <summary>
///     Represents the request to update a User.
/// </summary>
/// <param name="Email" example="jane.doe@example.com">New Email of the User, or null to leave it unchanged. Must be unique.</param>
/// <param name="FirstName" example="Jane">New First Name of the User, or null to leave it unchanged.</param>
/// <param name="LastName" example="Doe">New Last Name of the User, or null to leave it unchanged.</param>
public sealed record UpdateUserRequest(
    [EmailAddress, StringLength(254)] string? Email,
    [StringLength(User.NameMaxLength, MinimumLength = 1)] string? FirstName,
    [StringLength(User.NameMaxLength, MinimumLength = 1)] string? LastName);
