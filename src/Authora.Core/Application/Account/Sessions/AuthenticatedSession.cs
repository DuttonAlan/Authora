// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.Sessions;

/// <summary>
///     A valid Session and the User it belongs to.
/// </summary>
/// <param name="SessionId">Id of the Session.</param>
/// <param name="UserId">Id of the signed-in User.</param>
/// <param name="Email">Email of the User, shown e.g. on the consent screen.</param>
/// <param name="FirstName">First Name of the User.</param>
/// <param name="LastName">Last Name of the User.</param>
/// <param name="AuthenticatedAt">Point in time (UTC) when the User signed in.</param>
public sealed record AuthenticatedSession(
    Guid SessionId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTime AuthenticatedAt);
