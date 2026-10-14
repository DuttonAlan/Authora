// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Groups.GetMembers;

/// <summary>
///     Represents a single member in the response for getting the members of a Group.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-1b2c3d4e5f60">Id of the User.</param>
/// <param name="Email" example="jane.doe@example.com">Email of the User.</param>
/// <param name="FirstName" example="Jane">First Name of the User.</param>
/// <param name="LastName" example="Doe">Last Name of the User.</param>
/// <param name="State" example="Active">State of the User.</param>
public sealed record GetGroupMembersResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserState State);
