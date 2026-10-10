// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace Authora.Features.Users.Get;

/// <summary>
///     Provides the Endpoint to get a User.
/// </summary>
/// <param name="getUserService">The Service to get the User.</param>
[ApiController]
[Route("api/users")]
public sealed class GetUserController(GetUserService getUserService) : ControllerBase
{
    /// <summary>
    ///     Gets the User based on the given Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the User, or 404 Not Found if the User does not exist.</returns>
    [HttpGet("{id:guid}")]
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
