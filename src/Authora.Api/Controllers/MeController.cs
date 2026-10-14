// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Core.Application.Users.GetCurrent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint for the User of the Access Token, e.g. for the admin interface.
/// </summary>
/// <param name="getCurrentUserService">The Service to get the User and their Permissions.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/me")]
[Tags("Me")]
public sealed class MeController(GetCurrentUserService getCurrentUserService) : ControllerBase
{
    /// <summary>
    ///     Gets the User of the Access Token and the Names of their Permissions.
    /// </summary>
    /// <remarks>
    ///     Needs no Permission, only an Access Token with the Scope <c>authora.admin</c>, so a User without
    ///     Permissions can see that they have none. The Permissions are only for display: every endpoint checks its
    ///     Permission itself.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the User.</returns>
    /// <response code="200">Responds with the User and their Permissions.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<GetCurrentUserResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        // The Access Token handler always sets the subject, and only accepts tokens of Users who can sign in.
        var userId = Guid.Parse(User.FindFirst(AccessTokenClaimTypes.Subject)!.Value);

        var user = await getCurrentUserService.ExecuteAsync(userId, cancellationToken);

        return Ok(user);
    }
}
