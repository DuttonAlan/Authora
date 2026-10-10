// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace Authora.Features.Users.Update;

/// <summary>
///     Provides the Endpoint to update a User.
/// </summary>
/// <param name="updateUserService">The Service to update the User.</param>
[ApiController]
[Route("api/users")]
public sealed class UpdateUserController(UpdateUserService updateUserService) : ControllerBase
{
    /// <summary>
    ///     Updates the User based on the given Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the User was updated, or 404 Not Found if the User does not exist.</returns>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await updateUserService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }
}
