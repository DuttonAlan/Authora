// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Users.Get;

/// <summary>
///     Represents the response for getting a User.
/// </summary>
/// <param name="Id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
/// <param name="Email" example="jane.doe@example.com">Email of the User.</param>
/// <param name="FirstName" example="Jane">First Name of the User.</param>
/// <param name="LastName" example="Doe">Last Name of the User.</param>
/// <param name="State" example="Active">State of the User.</param>
/// <param name="FailedLoginCount" example="0">Number of failed login attempts in a row.</param>
/// <param name="LockoutEndAt">Point in time (UTC) until which the User is locked out, or null if the User is not locked out.</param>
/// <param name="LastLoginAt">Point in time (UTC) of the last successful login, or null if the User never logged in.</param>
/// <param name="CreatedAt">Point in time (UTC) when the User was created.</param>
/// <param name="ModifiedAt">Point in time (UTC) when the User was last modified, or null if it was never modified.</param>
/// <param name="Roles">Roles assigned to the User directly. The Roles the User has through Groups are returned by <c>GET /api/users/{id}/roles</c>.</param>
public sealed record GetUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserState State,
    int FailedLoginCount,
    DateTime? LockoutEndAt,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<UserRoleResponse> Roles);
