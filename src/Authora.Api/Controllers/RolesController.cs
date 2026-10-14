// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Roles.Create;
using Authora.Core.Application.Roles.Delete;
using Authora.Core.Application.Roles.Get;
using Authora.Core.Application.Roles.GetAll;
using Authora.Core.Application.Roles.GrantPermission;
using Authora.Core.Application.Roles.RevokePermission;
using Authora.Core.Application.Roles.Update;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do Role related operations.
/// </summary>
/// <param name="createRoleService">The Service to create the Role.</param>
/// <param name="deleteRoleService">The Service to delete the Role.</param>
/// <param name="getAllRolesService">The Service to get all Roles.</param>
/// <param name="getRoleService">The Service to get the Role.</param>
/// <param name="grantPermissionService">The Service to grant a Permission to the Role.</param>
/// <param name="revokePermissionService">The Service to revoke a Permission from the Role.</param>
/// <param name="updateRoleService">The Service to update the Role.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/roles")]
[Tags("Roles")]
public sealed class RolesController(
    CreateRoleService createRoleService,
    DeleteRoleService deleteRoleService,
    GetAllRolesService getAllRolesService,
    GetRoleService getRoleService,
    GrantPermissionService grantPermissionService,
    RevokePermissionService revokePermissionService,
    UpdateRoleService updateRoleService) : ControllerBase
{
    /// <summary>
    ///     Creates a Role.
    /// </summary>
    /// <remarks>
    ///     Leading and trailing whitespace is trimmed from all fields before the Role is saved.
    ///     New Roles have no Permissions.
    /// </remarks>
    /// <param name="request">The request to create the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the created Role.</returns>
    /// <response code="201">Responds with the Id of the created Role and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="409">Responds with problem details if the Name is already used by another Role.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateRoleResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.Create)]
    public async Task<IActionResult> Create(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var roleId = await createRoleService.ExecuteAsync(
            request,
            cancellationToken);

        var response = new CreateRoleResponse(roleId);

        return CreatedAtAction(nameof(Get), new { id = roleId }, response);
    }

    /// <summary>
    ///     Deletes the Role based on the given Id.
    /// </summary>
    /// <remarks>
    ///     The Role is also removed from all Users and Groups that have it.
    ///     This operation is idempotent: deleting a Role that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the Role was deleted, or if no Role with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Role is a System Role.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteRoleService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets all Roles, ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Roles.</returns>
    /// <response code="200">Responds with all Roles, or an empty list if no Roles exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllRolesResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await getAllRolesService.ExecuteAsync(cancellationToken);

        return Ok(roles);
    }

    /// <summary>
    ///     Gets the Role based on the given Id.
    /// </summary>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Role, or 404 Not Found if the Role does not exist.</returns>
    /// <response code="200">Responds with the Role matching the given Id, including its Permissions.</response>
    /// <response code="404">Responds with problem details if no Role with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetRoleResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.Read)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var role = await getRoleService.ExecuteAsync(id, cancellationToken);

        if (role is null)
        {
            return NotFound();
        }

        return Ok(role);
    }

    /// <summary>
    ///     Updates the Role based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged. An empty Description removes it.
    ///     Leading and trailing whitespace is trimmed from all set fields.
    ///     The Name of a System Role cannot be changed.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="request">The request to update the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the Role was updated.</returns>
    /// <response code="204">Responds with no content once the Role was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="404">Responds with problem details if no Role with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the new Name is already used, or the Role is a System Role and its Name would change.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        await updateRoleService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Grants a Permission to the Role.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: granting a Permission that is already granted also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="permissionId" example="0199d1a2-7c3e-7b4a-9f10-3fa85f64afa6">Id of the Permission.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Permission is granted.</returns>
    /// <response code="204">Responds with no content once the Permission was granted.</response>
    /// <response code="404">Responds with problem details if no Role or no Permission with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.GrantPermissions)]
    public async Task<IActionResult> GrantPermission(
        Guid id,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        await grantPermissionService.ExecuteAsync(id, permissionId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Revokes a Permission from the Role.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: revoking a Permission that is not granted also succeeds.
    ///     Permissions cannot be revoked from the system administrator Role.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="permissionId" example="0199d1a2-7c3e-7b4a-9f10-3fa85f64afa6">Id of the Permission.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Permission is revoked.</returns>
    /// <response code="204">Responds with no content once the Permission was revoked, or if it was not granted.</response>
    /// <response code="404">Responds with problem details if no Role with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Role is the system administrator Role.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Roles.GrantPermissions)]
    public async Task<IActionResult> RevokePermission(
        Guid id,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        await revokePermissionService.ExecuteAsync(id, permissionId, cancellationToken);

        return NoContent();
    }
}
