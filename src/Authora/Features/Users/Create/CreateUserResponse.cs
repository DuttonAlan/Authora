// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Features.Users.Create;

/// <summary>
///     Represents the response when creating a user.
/// </summary>
/// <param name="Id">The Id of the created user.</param>
public sealed record CreateUserResponse(Guid Id);
