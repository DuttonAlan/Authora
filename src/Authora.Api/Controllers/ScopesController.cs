// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Scopes.Create;
using Authora.Core.Application.Scopes.Delete;
using Authora.Core.Application.Scopes.Get;
using Authora.Core.Application.Scopes.GetAll;
using Authora.Core.Application.Scopes.Update;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do Scope related operations.
/// </summary>
/// <param name="createScopeService">The Service to create the Scope.</param>
/// <param name="deleteScopeService">The Service to delete the Scope.</param>
/// <param name="getAllScopesService">The Service to get all Scopes.</param>
/// <param name="getScopeService">The Service to get the Scope.</param>
/// <param name="updateScopeService">The Service to update the Scope.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/scopes")]
[Tags("Scopes")]
public sealed class ScopesController(
    CreateScopeService createScopeService,
    DeleteScopeService deleteScopeService,
    GetAllScopesService getAllScopesService,
    GetScopeService getScopeService,
    UpdateScopeService updateScopeService) : ControllerBase
{
    /// <summary>
    ///     Creates a Scope.
    /// </summary>
    /// <remarks>
    ///     Leading and trailing whitespace is trimmed from all fields before the Scope is saved.
    ///     The Name cannot be changed after creation, because Clients send it in their requests.
    /// </remarks>
    /// <param name="request">The request to create the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the created Scope.</returns>
    /// <response code="201">Responds with the Id of the created Scope and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="409">Responds with problem details if the Name is already used by another Scope.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateScopeResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Scopes.Create)]
    public async Task<IActionResult> Create(
        CreateScopeRequest request,
        CancellationToken cancellationToken)
    {
        var scopeId = await createScopeService.ExecuteAsync(
            request,
            cancellationToken);

        var response = new CreateScopeResponse(scopeId);

        return CreatedAtAction(nameof(Get), new { id = scopeId }, response);
    }

    /// <summary>
    ///     Deletes the Scope based on the given Id.
    /// </summary>
    /// <remarks>
    ///     A Scope that Clients are allowed to request can only be deleted with <c>force=true</c>,
    ///     which also removes it from these Clients.
    ///     This operation is idempotent: deleting a Scope that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="force" example="false">True to delete the Scope even if Clients use it.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the Scope was deleted, or if no Scope with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Scope is a System Scope, or Clients use it and force is not set.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Scopes.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] bool force = false)
    {
        await deleteScopeService.ExecuteAsync(id, force, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets all Scopes, ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Scopes.</returns>
    /// <response code="200">Responds with all Scopes, or an empty list if no Scopes exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllScopesResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Scopes.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var scopes = await getAllScopesService.ExecuteAsync(cancellationToken);

        return Ok(scopes);
    }

    /// <summary>
    ///     Gets the Scope based on the given Id.
    /// </summary>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Scope, or 404 Not Found if the Scope does not exist.</returns>
    /// <response code="200">Responds with the Scope matching the given Id.</response>
    /// <response code="404">Responds with problem details if no Scope with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetScopeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Scopes.Read)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = await getScopeService.ExecuteAsync(id, cancellationToken);

        if (scope is null)
        {
            return NotFound();
        }

        return Ok(scope);
    }

    /// <summary>
    ///     Updates the Scope based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged. An empty Description removes it.
    ///     Leading and trailing whitespace is trimmed from all set fields.
    ///     The Name cannot be changed; create a new Scope instead.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="request">The request to update the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the Scope was updated.</returns>
    /// <response code="204">Responds with no content once the Scope was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="404">Responds with problem details if no Scope with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Scopes.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateScopeRequest request,
        CancellationToken cancellationToken)
    {
        await updateScopeService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }
}
