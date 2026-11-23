// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Consents;
using Authora.Core.Application.Consents.GetAll;
using Authora.Core.Application.Consents.Revoke;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Paging;
using Authora.Core.Application.Users;
using Authora.Core.Application.Users.Activate;
using Authora.Core.Application.Users.AssignRole;
using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Deactivate;
using Authora.Core.Application.Users.Delete;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.GetGroups;
using Authora.Core.Application.Users.GetPermissions;
using Authora.Core.Application.Users.GetRoles;
using Authora.Core.Application.Users.MarkEmailVerified;
using Authora.Core.Application.Users.RemovePasskey;
using Authora.Core.Application.Users.RemoveRole;
using Authora.Core.Application.Users.ResetMfa;
using Authora.Core.Application.Users.RevokeSessions;
using Authora.Core.Application.Users.SendEmailVerification;
using Authora.Core.Application.Users.SendPasswordReset;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Application.Users.Unlock;
using Authora.Core.Application.Users.Update;
using Authora.Core.Domain.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do User related operations.
/// </summary>
/// <param name="activateUserService">The Service to activate the User.</param>
/// <param name="assignRoleService">The Service to assign a Role to the User.</param>
/// <param name="createUserService">The Service to create the User.</param>
/// <param name="deactivateUserService">The Service to deactivate the User.</param>
/// <param name="deleteUserService">The Service to delete the User.</param>
/// <param name="getAllUsersService">The Service to get all Users.</param>
/// <param name="getEffectivePermissionsService">The Service to get the effective Permissions of the User.</param>
/// <param name="getMfaStatusService">The Service to get the multi-factor authentication of the User.</param>
/// <param name="getUserConsentsService">The Service to get the Consents of the User.</param>
/// <param name="getUserGroupsService">The Service to get the Groups of the User.</param>
/// <param name="getUserRolesService">The Service to get the Roles of the User.</param>
/// <param name="getUserService">The Service to get the User.</param>
/// <param name="markEmailVerifiedService">The Service to mark the Email of the User as verified.</param>
/// <param name="removeRoleService">The Service to remove a Role from the User.</param>
/// <param name="removeUserPasskeyService">The Service to remove a passkey of the User.</param>
/// <param name="revokeConsentService">The Service to revoke a Consent of the User.</param>
/// <param name="resetMfaService">The Service to reset the multi-factor authentication of the User.</param>
/// <param name="revokeUserSessionsService">The Service to revoke all Sessions of the User.</param>
/// <param name="sendEmailVerificationService">The Service to send the User an email verification link.</param>
/// <param name="sendPasswordResetService">The Service to send the User a password reset link.</param>
/// <param name="setPasswordService">The Service to set the password of the User.</param>
/// <param name="unlockUserService">The Service to unlock the User.</param>
/// <param name="updateUserService">The Service to update the User.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/users")]
[Tags("Users")]
public sealed class UsersController(
    ActivateUserService activateUserService,
    AssignRoleService assignRoleService,
    CreateUserService createUserService,
    DeactivateUserService deactivateUserService,
    DeleteUserService deleteUserService,
    GetAllUsersService getAllUsersService,
    GetEffectivePermissionsService getEffectivePermissionsService,
    GetMfaStatusService getMfaStatusService,
    GetUserConsentsService getUserConsentsService,
    GetUserGroupsService getUserGroupsService,
    GetUserRolesService getUserRolesService,
    GetUserService getUserService,
    MarkEmailVerifiedService markEmailVerifiedService,
    RemoveRoleService removeRoleService,
    RemoveUserPasskeyService removeUserPasskeyService,
    RevokeConsentService revokeConsentService,
    ResetMfaService resetMfaService,
    RevokeUserSessionsService revokeUserSessionsService,
    SendEmailVerificationService sendEmailVerificationService,
    SendPasswordResetService sendPasswordResetService,
    SetPasswordService setPasswordService,
    UnlockUserService unlockUserService,
    UpdateUserService updateUserService) : ControllerBase
{
    /// <summary>
    ///     Creates a User.
    /// </summary>
    /// <remarks>
    ///     Leading and trailing whitespace is trimmed from all fields before the User is saved.
    ///     New Users are created in the active state, with an Email that is not verified yet. A verification link is sent
    ///     to it in the background (see <c>Authora:EmailVerification:SendAutomatically</c>).
    /// </remarks>
    /// <param name="request">The request to create the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the created User.</returns>
    /// <response code="201">Responds with the Id of the created User and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="409">Responds with problem details if the Email is already used by another User.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateUserResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Create)]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await createUserService.ExecuteAsync(
            request,
            cancellationToken);

        var response = new CreateUserResponse(userId);

        return CreatedAtAction(nameof(Get), new { id = userId }, response);
    }

    /// <summary>
    ///     Deletes the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     The User is not removed but set to the deleted state and kept for history. The Email and Name are
    ///     anonymized, so the Email can be used by a new User.
    ///     All Sessions and Refresh Tokens of the User become invalid.
    ///     This operation is idempotent: deleting a User that does not exist or is already deleted also succeeds.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the User was deleted, or if no User with the given Id exists.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="409">Responds with problem details if the User is the last administrator.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteUserService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets one page of Users, ordered by their Email.
    /// </summary>
    /// <remarks>
    ///     Deleted Users are only returned when filtering for the deleted state.
    /// </remarks>
    /// <param name="state" example="Active">Only return Users in this state. If missing, all Users that are not deleted are returned.</param>
    /// <param name="search" example="jane">Only return Users whose Email, First Name or Last Name contains this text (case-insensitive).</param>
    /// <param name="page" example="1">The 1-based number of the page.</param>
    /// <param name="pageSize" example="25">The number of Users per page, at most 100.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with one page of Users.</returns>
    /// <response code="200">Responds with the Users on the requested page and the total number of matching Users.</response>
    /// <response code="400">Responds with validation problem details if a query parameter is invalid.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<GetAllUsersResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] UserState? state,
        [FromQuery, StringLength(254)] string? search,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25)
    {
        var users = await getAllUsersService.ExecuteAsync(
            state,
            search,
            new PageRequest(page, pageSize),
            cancellationToken);

        return Ok(users);
    }

    /// <summary>
    ///     Gets the User based on the given Id.
    /// </summary>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the User, or 404 Not Found if the User does not exist.</returns>
    /// <response code="200">Responds with the User matching the given Id, including its directly assigned Roles.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetUserResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
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

    /// <summary>
    ///     Updates the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged.
    ///     Leading and trailing whitespace is trimmed from all set fields.
    ///     A different Email is no longer verified, and a verification link is sent to it in the background (see
    ///     <c>Authora:EmailVerification:SendAutomatically</c>); a change of upper and lower case only keeps the verification.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the User was updated.</returns>
    /// <response code="204">Responds with no content once the User was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted, or the new Email is already used by another User.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        await updateUserService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Deactivates the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     A deactivated User cannot sign in, and all of its Sessions and Refresh Tokens become invalid.
    ///     The data of the User is kept. Deactivating a User that is already deactivated also succeeds.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the User is deactivated.</returns>
    /// <response code="204">Responds with no content once the User was deactivated.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted or the last administrator.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ManageAccess)]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deactivateUserService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Activates the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Activating a User that is already active also succeeds. Deleted Users cannot be activated.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the User is activated.</returns>
    /// <response code="204">Responds with no content once the User was activated.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ManageAccess)]
    public async Task<IActionResult> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await activateUserService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Unlocks the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Resets the failed login attempts and ends the lockout of the User.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the User is unlocked.</returns>
    /// <response code="204">Responds with no content once the User was unlocked.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ManageAccess)]
    public async Task<IActionResult> Unlock(
        Guid id,
        CancellationToken cancellationToken)
    {
        await unlockUserService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Signs the User out everywhere.
    /// </summary>
    /// <remarks>
    ///     All Sessions and Refresh Tokens of the User become invalid.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Sessions are revoked.</returns>
    /// <response code="204">Responds with no content once the Sessions were revoked.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted. Its Sessions were already revoked on deletion.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/revoke-sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ManageAccess)]
    public async Task<IActionResult> RevokeSessions(
        Guid id,
        CancellationToken cancellationToken)
    {
        await revokeUserSessionsService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Sets the password of the User.
    /// </summary>
    /// <remarks>
    ///     The password must be 12 to 128 characters long. Setting it signs the User out everywhere:
    ///     all Sessions end and all tokens issued for the User become invalid.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="request">The request with the new password.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the password is set.</returns>
    /// <response code="204">Responds with no content once the password was set.</response>
    /// <response code="400">Responds with validation problem details if the password is too short or too long.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/password")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.SetPassword)]
    public async Task<IActionResult> SetPassword(
        Guid id,
        SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await setPasswordService.ExecuteAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Sends the User a link to choose a new password.
    /// </summary>
    /// <remarks>
    ///     The email goes to the User's current address, and the link works for <c>Authora:PasswordReset:LinkLifetime</c>
    ///     (30 minutes by default). Earlier reset links of the User stop working. The current password keeps working until
    ///     the User sets a new one with the link; that signs the User out everywhere and ends a lockout.
    ///     Needs email delivery (<c>Authora:Email:Host</c>).
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the mail server accepted the email.</returns>
    /// <response code="204">Responds with no content once the mail server accepted the email.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if email delivery is not configured, or the User is deleted or deactivated.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    /// <response code="502">Responds with problem details if the mail server could not be reached or rejected the email.</response>
    [HttpPost("{id:guid}/password-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [Authorize(PermissionNames.Users.SetPassword)]
    public async Task<IActionResult> SendPasswordReset(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sendPasswordResetService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Sends the User a link to confirm their Email.
    /// </summary>
    /// <remarks>
    ///     A link is already sent by itself when a User is created or their Email changes (unless
    ///     <c>Authora:EmailVerification:SendAutomatically</c> is false); use this to send another one, e.g. after it
    ///     expired. The link works for <c>Authora:EmailVerification:LinkLifetime</c> (24 hours by default), and earlier
    ///     verification links of the User stop working. Needs email delivery (<c>Authora:Email:Host</c>).
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the mail server accepted the email.</returns>
    /// <response code="204">Responds with no content once the mail server accepted the email.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if email delivery is not configured, the User is deleted or deactivated, or their Email is already verified.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    /// <response code="502">Responds with problem details if the mail server could not be reached or rejected the email.</response>
    [HttpPost("{id:guid}/email-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [Authorize(PermissionNames.Users.Update)]
    public async Task<IActionResult> SendEmailVerification(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sendEmailVerificationService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Marks the Email of the User as verified without a link.
    /// </summary>
    /// <remarks>
    ///     Applications may trust a verified Email (<c>email_verified</c>) to belong to the User, so only mark addresses
    ///     the User is known to own. Open verification links of the User stop working. Marking an Email that is already
    ///     verified also succeeds and keeps the original point in time. Changing the Email resets the verification.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Email is marked as verified.</returns>
    /// <response code="204">Responds with no content once the Email is marked as verified.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/email-verified")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Update)]
    public async Task<IActionResult> MarkEmailVerified(
        Guid id,
        CancellationToken cancellationToken)
    {
        await markEmailVerifiedService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets the effective Roles of the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Returns the Roles assigned to the User directly and the Roles of the Groups of the User.
    ///     For each Role, <c>isDirect</c> and <c>groups</c> tell where the User has it from.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Roles of the User.</returns>
    /// <response code="200">Responds with the Roles of the User ordered by their Name, or an empty list if the User has no Roles.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/roles")]
    [ProducesResponseType<IReadOnlyList<EffectiveRoleResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetRoles(
        Guid id,
        CancellationToken cancellationToken)
    {
        var roles = await getUserRolesService.ExecuteAsync(id, cancellationToken);

        return Ok(roles);
    }

    /// <summary>
    ///     Assigns a Role to the User.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: assigning a Role that is already assigned also succeeds.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="roleId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Role is assigned.</returns>
    /// <response code="204">Responds with no content once the Role was assigned.</response>
    /// <response code="403">Responds with problem details if the Role is the administrator Role, or the Role or the User has Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User or no Role with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.AssignRoles)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await assignRoleService.ExecuteAsync(id, roleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes a Role from the User.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: removing a Role that is not assigned also succeeds.
    ///     The administrator Role cannot be removed from the last administrator.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="roleId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Role is removed.</returns>
    /// <response code="204">Responds with no content once the Role was removed, or if the Role was not assigned.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted, or the User is the last administrator.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.AssignRoles)]
    public async Task<IActionResult> RemoveRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await removeRoleService.ExecuteAsync(id, roleId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets the effective Permissions of the User.
    /// </summary>
    /// <remarks>
    ///     Returns the union of the Permissions of all Roles of the User, assigned directly or through a Group,
    ///     and for each Permission the Roles it comes from and where the User has each of these Roles from.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the effective Permissions of the User.</returns>
    /// <response code="200">Responds with the Permissions of the User ordered by their Name, or an empty list if the User has none.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/permissions")]
    [ProducesResponseType<IReadOnlyList<GetEffectivePermissionsResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetPermissions(
        Guid id,
        CancellationToken cancellationToken)
    {
        var permissions = await getEffectivePermissionsService.ExecuteAsync(id, cancellationToken);

        return Ok(permissions);
    }

    /// <summary>
    ///     Gets the Groups of the User based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Memberships are managed through <c>/api/groups/{id}/members</c>.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Groups of the User.</returns>
    /// <response code="200">Responds with the Groups of the User ordered by their Name, or an empty list if the User is in no Group.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/groups")]
    [ProducesResponseType<IReadOnlyList<UserGroupResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetGroups(
        Guid id,
        CancellationToken cancellationToken)
    {
        var groups = await getUserGroupsService.ExecuteAsync(id, cancellationToken);

        return Ok(groups);
    }

    /// <summary>
    ///     Gets the Consents of the User: the third-party Clients the User approved, with the approved Scopes.
    /// </summary>
    /// <remarks>
    ///     The User is not asked again while a Client requests only approved Scopes, unless it sends
    ///     <c>prompt=consent</c>. First-party Clients need no Consent.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Consents of the User.</returns>
    /// <response code="200">Responds with the Consents of the User ordered by the Name of the Client, or an empty list if the User approved no Client.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/consents")]
    [ProducesResponseType<IReadOnlyList<ConsentResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetConsents(
        Guid id,
        CancellationToken cancellationToken)
    {
        var consents = await getUserConsentsService.ExecuteAsync(id, cancellationToken);

        return Ok(consents);
    }

    /// <summary>
    ///     Gets the multi-factor authentication of the User: the authenticator app, the recovery codes and the passkeys.
    /// </summary>
    /// <remarks>
    ///     Neither the secret of the app, the recovery codes nor the keys of the passkeys are ever returned; only when they were set up and last
    ///     used, how many codes are left, and the name and kind of each passkey. An unfinished setup does not count as an authenticator app.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the multi-factor authentication of the User.</returns>
    /// <response code="200">Responds with the multi-factor authentication of the User; <c>enabled</c> is <c>false</c> if the User signs in with the password only.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}/mfa")]
    [ProducesResponseType<MfaStatusResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.Read)]
    public async Task<IActionResult> GetMfa(
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await getMfaStatusService.ExecuteAsync(id, cancellationToken);

        return Ok(status);
    }

    /// <summary>
    ///     Resets the multi-factor authentication of the User.
    /// </summary>
    /// <remarks>
    ///     Removes the authenticator app, the recovery codes and the passkeys, e.g. after the User lost them; the password alone then
    ///     signs the User in until they set up a new second factor. All Sessions and tokens of the User are revoked.
    ///     This operation is idempotent: resetting a User without multi-factor authentication also succeeds and changes
    ///     nothing.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the multi-factor authentication is reset.</returns>
    /// <response code="204">Responds with no content once the multi-factor authentication was reset.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the User is deleted, or was changed by another request at the same time.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/mfa")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ResetMfa)]
    public async Task<IActionResult> ResetMfa(
        Guid id,
        CancellationToken cancellationToken)
    {
        await resetMfaService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes a passkey of the User.
    /// </summary>
    /// <remarks>
    ///     E.g. one on a lost security key. The other passkeys and the authenticator app stay. All Sessions and tokens of
    ///     the User are revoked, so whoever signed in with the passkey is signed out. The passkeys of the User are listed
    ///     in <c>GET /api/users/{id}/mfa</c>.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="passkeyId" example="0199d1a2-7c3e-7b4a-9f10-1c963f66afa6">Id of the passkey.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the passkey is removed.</returns>
    /// <response code="204">Responds with no content once the passkey was removed.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists, or the User has no passkey with the given Id.</response>
    /// <response code="409">Responds with problem details if the User is deleted, or was changed by another request at the same time.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/passkeys/{passkeyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ResetMfa)]
    public async Task<IActionResult> RemovePasskey(
        Guid id,
        Guid passkeyId,
        CancellationToken cancellationToken)
    {
        await removeUserPasskeyService.ExecuteAsync(id, passkeyId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Revokes the Consent of the User for a Client.
    /// </summary>
    /// <remarks>
    ///     The Grants of the User for the Client are revoked as well, so all Access and Refresh Tokens the Client holds
    ///     for the User become invalid, and the User is asked for consent again.
    ///     This operation is idempotent: revoking a Consent that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">Id of the User.</param>
    /// <param name="clientId" example="0199d1a2-7c3e-7b4a-9f10-1c963f66afa6">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Consent is revoked.</returns>
    /// <response code="204">Responds with no content once the Consent was revoked, or if the User has no Consent for the Client.</response>
    /// <response code="403">Responds with problem details if the User has the administrator Role or Permissions that the caller does not hold. Administrators are exempt.</response>
    /// <response code="404">Responds with problem details if no User with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/consents/{clientId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Users.ManageAccess)]
    public async Task<IActionResult> RevokeConsent(
        Guid id,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        await revokeConsentService.ExecuteAsync(id, clientId, cancellationToken);

        return NoContent();
    }
}
