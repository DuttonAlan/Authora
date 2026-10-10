// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Users.Get;

/// <summary>
///     Represents the response for getting a User.
/// </summary>
/// <param name="Id">Id of the User.</param>
/// <param name="Email">Email of the User.</param>
/// <param name="FirstName">First Name of the User.</param>
/// <param name="LastName">Last Name of the User.</param>
public sealed record GetUserResponse(Guid Id, string Email, string FirstName, string LastName);
