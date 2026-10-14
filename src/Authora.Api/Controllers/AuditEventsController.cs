// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authorization;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoint to search the audit log (US-034).
/// </summary>
/// <param name="searchAuditEventsService">The Service to search the audit log.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/audit-events")]
[Tags("Audit")]
public sealed class AuditEventsController(SearchAuditEventsService searchAuditEventsService) : ControllerBase
{
    /// <summary>
    ///     Searches the audit log, newest events first.
    /// </summary>
    /// <remarks>
    ///     All given criteria must match. Events are kept for <c>Authora:Cleanup:AuditRetention</c> and cannot be
    ///     changed or deleted through the API. Sign-in events have the User as their target, because nobody is signed
    ///     in yet when they occur.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <param name="type" example="user">A Type (e.g. <c>user.created</c>) or an area (e.g. <c>user</c> for all <c>user.*</c> events).</param>
    /// <param name="actorUserId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Only events caused by this User.</param>
    /// <param name="targetType" example="user">Only events about this kind of object: <c>user</c>, <c>role</c>, <c>group</c>, <c>scope</c>, <c>client</c> or <c>signing-key</c>.</param>
    /// <param name="targetId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Only events about the object with this Id.</param>
    /// <param name="outcome" example="Failed">Only events with this outcome.</param>
    /// <param name="from" example="2026-10-01T00:00:00Z">Only events at or after this point in time.</param>
    /// <param name="to" example="2026-11-01T00:00:00Z">Only events before this point in time.</param>
    /// <param name="page" example="1">The 1-based number of the page.</param>
    /// <param name="pageSize" example="50">The number of events per page, at most 200.</param>
    /// <returns>200 OK with one page of events.</returns>
    /// <response code="200">Responds with the events on the requested page and the total number of matching events.</response>
    /// <response code="400">Responds with validation problem details if a query parameter is invalid, or <c>from</c> is not before <c>to</c>.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AuditEventResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Audit.Read)]
    public async Task<IActionResult> Search(
        CancellationToken cancellationToken,
        [FromQuery, StringLength(100), RegularExpression("^[a-z0-9.-]+$")] string? type = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery, StringLength(50), RegularExpression("^[a-z0-9-]+$")] string? targetType = null,
        [FromQuery, StringLength(200)] string? targetId = null,
        [FromQuery] AuditOutcome? outcome = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50)
    {
        var events = await searchAuditEventsService.ExecuteAsync(
            new AuditEventFilter(type, actorUserId, targetType, targetId, outcome, from, to),
            new PageRequest(page, pageSize),
            cancellationToken);

        return Ok(events);
    }
}
