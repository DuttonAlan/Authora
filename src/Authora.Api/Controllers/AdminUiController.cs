// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.AdminUi;
using Authora.Core.Application.AdminUi.GetSettings;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the settings the admin interface signs in with (US-033).
/// </summary>
/// <param name="getAdminUiSettingsService">The Service to get the settings.</param>
/// <param name="options">Whether the admin interface is enabled.</param>
[ApiController]
[Tags("Admin UI")]
public sealed class AdminUiController(
    GetAdminUiSettingsService getAdminUiSettingsService,
    IOptions<AdminUiOptions> options) : ControllerBase
{
    /// <summary>
    ///     Gets the Issuer, the Client Id, the Redirect URI and the Scope of the admin interface.
    /// </summary>
    /// <remarks>
    ///     Public, like the discovery document: the Client is a Public Client, so its Id is no secret.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the settings, or 404 Not Found.</returns>
    /// <response code="200">Responds with the settings of the admin interface.</response>
    /// <response code="404">Responds with problem details if the admin interface is disabled, or its Client was disabled or deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("/admin/settings")]
    [ProducesResponseType<AdminUiSettingsResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        Response.Headers[HeaderNames.CacheControl] = "no-store";

        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var settings = await getAdminUiSettingsService.ExecuteAsync(cancellationToken);

        if (settings is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"No enabled Public Client has the Redirect URI of the admin interface. Enable the Client "
                    + $"'{AdminUiOptions.ClientName}', or restart Authora to create it again.");
        }

        return Ok(settings);
    }
}
