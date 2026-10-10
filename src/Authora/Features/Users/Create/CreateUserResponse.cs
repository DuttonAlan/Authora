// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Features.Users.Create;

/// <summary>
///     Represents the response for creating a User.
/// </summary>
/// <param name="Id">Id of the created User.</param>
public sealed record CreateUserResponse(Guid Id);
