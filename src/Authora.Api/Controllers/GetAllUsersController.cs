// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.GetAll;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to get all Users.
/// </summary>
/// <param name="getAllUsersService">The Service to get all Users.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class GetAllUsersController(GetAllUsersService getAllUsersService) : ControllerBase
{
    /// <summary>
    ///     Gets all Users.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Users.</returns>
    /// <response code="200">Responds with all Users, or an empty list if no Users exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllUsersResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await getAllUsersService.ExecuteAsync(cancellationToken);

        return Ok(users);
    }
}
