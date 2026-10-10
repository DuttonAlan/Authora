// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Features.Users.Create;

/// <summary>
///     Represents the request to create a User.
/// </summary>
/// <param name="Email">The Email of the User.</param>
/// <param name="FirstName">The First Name of the User.</param>
/// <param name="LastName">The Last Name of the User.</param>
public sealed record CreateUserRequest(string Email, string FirstName, string LastName);
