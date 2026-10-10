// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.GetAll;

/// <summary>
///     Represents a single User in the response for getting all Users.
/// </summary>
/// <param name="Id">Id of the User.</param>
/// <param name="Email">Email of the User.</param>
/// <param name="FirstName">First Name of the User.</param>
/// <param name="LastName">Last Name of the User.</param>
public sealed record GetAllUsersResponse(Guid Id, string Email, string FirstName, string LastName);
