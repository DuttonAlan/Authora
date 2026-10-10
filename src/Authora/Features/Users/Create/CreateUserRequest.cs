// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Features.Users.Create;

/// <summary>
///     Represents the request to create a User.
/// </summary>
/// <param name="Email">Email of the User.</param>
/// <param name="FirstName">First Name of the User.</param>
/// <param name="LastName">Last Name of the User.</param>
public sealed record CreateUserRequest(string Email, string FirstName, string LastName);
