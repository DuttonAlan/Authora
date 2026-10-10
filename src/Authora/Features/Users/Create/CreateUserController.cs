// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace Authora.Features.Users.Create;

/// <summary>
///     Includes the Endpoint to create a User.
/// </summary>
/// <param name="createUserService"></param>
[ApiController]
[Route("api/users")]
public sealed class CreateUserController(CreateUserService createUserService) : ControllerBase
{
    /// <summary>
    ///     Creates a User.
    /// </summary>
    /// <param name="request">The request to create a User.</param>
    /// <param name="cancellationToken">A CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created User.</returns>
    [HttpPost]
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

