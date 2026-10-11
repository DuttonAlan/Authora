// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users.Create;

/// <summary>
///     Represents the request to create a User.
/// </summary>
/// <param name="Email" example="jane.doe@example.com">Email of the User. Must be unique.</param>
/// <param name="FirstName" example="Jane">First Name of the User.</param>
/// <param name="LastName" example="Doe">Last Name of the User.</param>
public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(User.NameMaxLength, MinimumLength = 1)] string FirstName,
    [Required, StringLength(User.NameMaxLength, MinimumLength = 1)] string LastName);
