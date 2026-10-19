// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authorization;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Audit.Verify;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to search the audit log and to verify its hash chain.
/// </summary>
/// <param name="searchAuditEventsService">The Service to search the audit log.</param>
/// <param name="verifyAuditChainService">The Service to verify the hash chain of the audit log.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/audit-events")]
[Tags("Audit")]
public sealed class AuditEventsController(
    SearchAuditEventsService searchAuditEventsService,
    VerifyAuditChainService verifyAuditChainService) : ControllerBase
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

    /// <summary>
    ///     Verifies the hash chain of the audit log.
    /// </summary>
    /// <remarks>
    ///     New events are sealed into the chain every few seconds (<c>Authora:Audit:SealInterval</c>). The chain
    ///     detects sealed events that were changed or deleted in the database, and events that were inserted there
    ///     without being sealed. Someone with write access to the database could rebuild the whole chain, so compare
    ///     the result with a checkpoint (Sequence and hash) from the application logs, which the sealing writes after
    ///     every batch. Reads every event, so it can take a while.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <param name="checkpointSequence" example="1532">The Sequence of a checkpoint from the application logs.</param>
    /// <param name="checkpointHash" example="9f2c…">The hash of the checkpoint. Required with <c>checkpointSequence</c>.</param>
    /// <returns>200 OK with the result, also if the chain is broken.</returns>
    /// <response code="200">Responds with the result. <c>isIntact</c> is false if the chain is broken, events are not sealed, or the checkpoint does not match.</response>
    /// <response code="400">Responds with validation problem details if only one part of the checkpoint is given or a part is malformed.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("verification")]
    [ProducesResponseType<AuditChainVerificationResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Audit.Verify)]
    public async Task<IActionResult> Verify(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, long.MaxValue)] long? checkpointSequence = null,
        [FromQuery, RegularExpression("^[0-9a-fA-F]{64}$")] string? checkpointHash = null)
    {
        var result = await verifyAuditChainService.ExecuteAsync(checkpointSequence, checkpointHash, cancellationToken);

        return Ok(result);
    }
}
