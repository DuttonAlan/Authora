// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Create;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to create a User.
/// </summary>
/// <param name="createUserService">The Service to create the User.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class CreateUserController(CreateUserService createUserService) : ControllerBase
{
    /// <summary>
    ///     Creates a User.
    /// </summary>
    /// <remarks>
    ///     Leading and trailing whitespace is trimmed from all fields before the User is saved.
    ///     New Users are created in the active state.
    /// </remarks>
    /// <param name="request">The request to create the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the created User.</returns>
    /// <response code="201">Responds with the Id of the created User and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await createUserService.ExecuteAsync(
            request,
            cancellationToken);

        var response = new CreateUserResponse(userId);

        return Created($"/api/users/{userId}", response);
    }
}

