// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Get;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to get a User.
/// </summary>
/// <param name="getUserService">The Service to get the User.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class GetUserController(GetUserService getUserService) : ControllerBase
{
    /// <summary>
    ///     Gets the User based on the given Id.
    /// </summary>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the User, or 404 Not Found if the User does not exist.</returns>
    /// <response code="200">Responds with the User matching the given Id.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetUserResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await getUserService.ExecuteAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }
}
