// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Delete;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to delete a User.
/// </summary>
/// <param name="deleteUserService">The Service to delete the User.</param>
[ApiController]
[Route("api/users")]
[Tags("Users")]
public sealed class DeleteUserController(DeleteUserService deleteUserService) : ControllerBase
{
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
}
