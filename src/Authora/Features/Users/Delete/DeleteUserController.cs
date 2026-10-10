// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace Authora.Features.Users.Delete;

/// <summary>
///     Provides the Endpoint to delete a User.
/// </summary>
/// <param name="deleteUserService">The Service to delete the User.</param>
[ApiController]
[Route("api/users")]
public sealed class DeleteUserController(DeleteUserService deleteUserService) : ControllerBase
{
    /// <summary>
    ///     Deletes the User based on the given Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the User was deleted, or 404 Not Found if the User does not exist.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await deleteUserService.ExecuteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
