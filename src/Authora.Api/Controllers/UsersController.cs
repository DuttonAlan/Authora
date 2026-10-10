// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Delete;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.Update;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do User related operations.
/// </summary>
/// <param name="createUserService">The Service to create the User.</param>
/// <param name="deleteUserService">The Service to delete the User.</param>
/// <param name="getAllUsersService">The Service to get all Users.</param>
/// <param name="getUserService">The Service to get the User.</param>
/// <param name="updateUserService">The Service to update the User.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class UsersController(
    CreateUserService createUserService,
    DeleteUserService deleteUserService,
    GetAllUsersService getAllUsersService,
    GetUserService getUserService,
    UpdateUserService updateUserService) : ControllerBase
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

        return CreatedAtAction(nameof(Get), new { id = userId }, response);
    }

    /// <summary>
    ///     Deletes the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: deleting a User that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the User was deleted, or if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteUserService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

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

    /// <summary>
    ///     Updates the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged.
    ///     Leading and trailing whitespace is trimmed from all set fields.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the User was updated.</returns>
    /// <response code="204">Responds with no content once the User was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred, including when no User with the given Id exists.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        await updateUserService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }
}
