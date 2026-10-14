// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Groups.AddMember;
using Authora.Core.Application.Groups.AssignRole;
using Authora.Core.Application.Groups.Create;
using Authora.Core.Application.Groups.Delete;
using Authora.Core.Application.Groups.Get;
using Authora.Core.Application.Groups.GetAll;
using Authora.Core.Application.Groups.GetMembers;
using Authora.Core.Application.Groups.RemoveMember;
using Authora.Core.Application.Groups.RemoveRole;
using Authora.Core.Application.Groups.Update;
using Authora.Core.Application.Paging;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do Group related operations.
/// </summary>
/// <param name="addGroupMemberService">The Service to add a User to the Group.</param>
/// <param name="assignGroupRoleService">The Service to assign a Role to the Group.</param>
/// <param name="createGroupService">The Service to create the Group.</param>
/// <param name="deleteGroupService">The Service to delete the Group.</param>
/// <param name="getAllGroupsService">The Service to get all Groups.</param>
/// <param name="getGroupMembersService">The Service to get the members of the Group.</param>
/// <param name="getGroupService">The Service to get the Group.</param>
/// <param name="removeGroupMemberService">The Service to remove a User from the Group.</param>
/// <param name="removeGroupRoleService">The Service to remove a Role from the Group.</param>
/// <param name="updateGroupService">The Service to update the Group.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/groups")]
[Tags("Groups")]
public sealed class GroupsController(
    AddGroupMemberService addGroupMemberService,
    AssignGroupRoleService assignGroupRoleService,
    CreateGroupService createGroupService,
    DeleteGroupService deleteGroupService,
    GetAllGroupsService getAllGroupsService,
    GetGroupMembersService getGroupMembersService,
    GetGroupService getGroupService,
    RemoveGroupMemberService removeGroupMemberService,
    RemoveGroupRoleService removeGroupRoleService,
    UpdateGroupService updateGroupService) : ControllerBase
{
    /// <summary>
    ///     Creates a Group.
    /// </summary>
    /// <remarks>
    ///     Leading and trailing whitespace is trimmed from all fields before the Group is saved.
    ///     New Groups have no Roles and no members.
    /// </remarks>
    /// <param name="request">The request to create the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the created Group.</returns>
    /// <response code="201">Responds with the Id of the created Group and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="409">Responds with problem details if the Name is already used by another Group.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateGroupResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Create)]
    public async Task<IActionResult> Create(
        CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var groupId = await createGroupService.ExecuteAsync(
            request,
            cancellationToken);

        var response = new CreateGroupResponse(groupId);

        return CreatedAtAction(nameof(Get), new { id = groupId }, response);
    }

    /// <summary>
    ///     Deletes the Group based on the given Id.
    /// </summary>
    /// <remarks>
    ///     All memberships end, so the members lose the Roles of the Group on their next request, unless they have
    ///     them directly or through another Group. The Users themselves are not changed.
    ///     This operation is idempotent: deleting a Group that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the Group was deleted, or if no Group with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteGroupService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets all Groups, ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Groups.</returns>
    /// <response code="200">Responds with all Groups, or an empty list if no Groups exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllGroupsResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var groups = await getAllGroupsService.ExecuteAsync(cancellationToken);

        return Ok(groups);
    }

    /// <summary>
    ///     Gets the Group based on the given Id.
    /// </summary>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Group, or 404 Not Found if the Group does not exist.</returns>
    /// <response code="200">Responds with the Group matching the given Id, including its Roles.</response>
    /// <response code="404">Responds with problem details if no Group with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetGroupResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Read)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var group = await getGroupService.ExecuteAsync(id, cancellationToken);

        if (group is null)
        {
            return NotFound();
        }

        return Ok(group);
    }

    /// <summary>
    ///     Updates the Group based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged. An empty Description removes it.
    ///     Leading and trailing whitespace is trimmed from all set fields.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="request">The request to update the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the Group was updated.</returns>
    /// <response code="204">Responds with no content once the Group was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="404">Responds with problem details if no Group with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the new Name is already used by another Group.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateGroupRequest request,
        CancellationToken cancellationToken)
    {
        await updateGroupService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Assigns a Role to the Group, so every member has it.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: assigning a Role that is already assigned also succeeds.
    ///     The administrator Role can only be assigned to Users directly.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="roleId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Role is assigned.</returns>
    /// <response code="204">Responds with no content once the Role was assigned.</response>
    /// <response code="404">Responds with problem details if no Group or no Role with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Role is the administrator Role.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.AssignRoles)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await assignGroupRoleService.ExecuteAsync(id, roleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes a Role from the Group.
    /// </summary>
    /// <remarks>
    ///     The members lose the Role on their next request, unless they have it directly or through another Group.
    ///     This operation is idempotent: removing a Role that is not assigned also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="roleId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Role is removed.</returns>
    /// <response code="204">Responds with no content once the Role was removed, or if the Role was not assigned.</response>
    /// <response code="404">Responds with problem details if no Group with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.AssignRoles)]
    public async Task<IActionResult> RemoveRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await removeGroupRoleService.ExecuteAsync(id, roleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets one page of the members of the Group, ordered by their Email.
    /// </summary>
    /// <remarks>
    ///     Deleted Users are not returned.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <param name="page" example="1">The 1-based number of the page.</param>
    /// <param name="pageSize" example="25">The number of members per page, at most 100.</param>
    /// <returns>200 OK with one page of members.</returns>
    /// <response code="200">Responds with the members on the requested page and the total number of members.</response>
    /// <response code="400">Responds with validation problem details if a query parameter is invalid.</response>
    /// <response code="404">Responds with problem details if no Group with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType<PagedResponse<GetGroupMembersResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.Read)]
    public async Task<IActionResult> GetMembers(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var members = await getGroupMembersService.ExecuteAsync(
            id,
            new PageRequest(page, pageSize),
            cancellationToken);

        return Ok(members);
    }

    /// <summary>
    ///     Adds a User to the Group, so the User has all Roles of the Group.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: adding a User who is already a member also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="userId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the User is a member.</returns>
    /// <response code="204">Responds with no content once the User was added.</response>
    /// <response code="404">Responds with problem details if no Group or no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.ManageMembers)]
    public async Task<IActionResult> AddMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await addGroupMemberService.ExecuteAsync(id, userId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes a User from the Group.
    /// </summary>
    /// <remarks>
    ///     The User loses the Roles of the Group on their next request, unless they have them directly or through
    ///     another Group. This operation is idempotent: removing a User who is not a member also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the Group.</param>
    /// <param name="userId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the User is no longer a member.</returns>
    /// <response code="204">Responds with no content once the User was removed, or if the User was not a member.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Groups.ManageMembers)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await removeGroupMemberService.ExecuteAsync(id, userId, cancellationToken);

        return NoContent();
    }
}
