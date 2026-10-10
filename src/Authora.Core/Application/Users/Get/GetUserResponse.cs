// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.Get;

/// <summary>
///     Represents the response for getting a User.
/// </summary>
/// <param name="Id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
/// <param name="Email" example="jane.doe@example.com">Email of the User.</param>
/// <param name="FirstName" example="Jane">First Name of the User.</param>
/// <param name="LastName" example="Doe">Last Name of the User.</param>
public sealed record GetUserResponse(Guid Id, string Email, string FirstName, string LastName);
