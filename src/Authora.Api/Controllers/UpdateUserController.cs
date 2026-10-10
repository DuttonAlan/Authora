// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Update;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to update a User.
/// </summary>
/// <param name="updateUserService">The Service to update the User.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class UpdateUserController(UpdateUserService updateUserService) : ControllerBase
{
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
