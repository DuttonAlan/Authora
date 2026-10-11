// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Permissions.GetAll;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to read Permissions.
/// </summary>
/// <remarks>
///     Permissions are defined in code and seeded, so they cannot be created, changed or deleted through the API.
/// </remarks>
/// <param name="getAllPermissionsService">The Service to get all Permissions.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/permissions")]
[Tags("Permissions")]
public sealed class PermissionsController(GetAllPermissionsService getAllPermissionsService) : ControllerBase
{
    /// <summary>
    ///     Gets all Permissions, ordered by their Name.
    /// </summary>
    /// <remarks>
    ///     Use the Ids to grant Permissions to Roles.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Permissions.</returns>
    /// <response code="200">Responds with all Permissions, or an empty list if no Permissions exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllPermissionsResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Permissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var permissions = await getAllPermissionsService.ExecuteAsync(cancellationToken);

        return Ok(permissions);
    }
}
