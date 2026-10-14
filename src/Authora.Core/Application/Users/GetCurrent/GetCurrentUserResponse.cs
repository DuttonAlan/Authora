// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.GetCurrent;

/// <summary>
///     The User of the Access Token and what the User may do in the admin API.
/// </summary>
/// <param name="Id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
/// <param name="Email" example="admin@example.com">Email of the User.</param>
/// <param name="FirstName" example="Ada">First Name of the User.</param>
/// <param name="LastName" example="Lovelace">Last Name of the User.</param>
/// <param name="Permissions" example="[&quot;users.read&quot;, &quot;roles.read&quot;]">
///     Names of the Permissions of the User, ordered by Name. The administrator Role has all Permissions.
/// </param>
public sealed record GetCurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Permissions);
