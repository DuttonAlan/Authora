// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients;
using Authora.Core.Application.Clients.AddRedirectUri;
using Authora.Core.Application.Clients.AllowScope;
using Authora.Core.Application.Clients.CreateSecret;
using Authora.Core.Application.Clients.Delete;
using Authora.Core.Application.Clients.Disable;
using Authora.Core.Application.Clients.DisallowScope;
using Authora.Core.Application.Clients.Enable;
using Authora.Core.Application.Clients.Get;
using Authora.Core.Application.Clients.GetAll;
using Authora.Core.Application.Clients.Register;
using Authora.Core.Application.Clients.RemoveRedirectUri;
using Authora.Core.Application.Clients.RevokeSecret;
using Authora.Core.Application.Clients.ServeScope;
using Authora.Core.Application.Clients.StopServingScope;
using Authora.Core.Application.Clients.Update;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to do Client related operations.
/// </summary>
/// <param name="addRedirectUriService">The Service to register a Redirect URI.</param>
/// <param name="allowScopeService">The Service to allow a Scope.</param>
/// <param name="createClientSecretService">The Service to create a Secret.</param>
/// <param name="deleteClientService">The Service to delete the Client.</param>
/// <param name="disableClientService">The Service to disable the Client.</param>
/// <param name="disallowScopeService">The Service to disallow a Scope.</param>
/// <param name="enableClientService">The Service to enable the Client.</param>
/// <param name="getAllClientsService">The Service to get all Clients.</param>
/// <param name="getClientService">The Service to get the Client.</param>
/// <param name="registerClientService">The Service to register the Client.</param>
/// <param name="removeRedirectUriService">The Service to remove a Redirect URI.</param>
/// <param name="revokeClientSecretService">The Service to revoke a Secret.</param>
/// <param name="serveScopeService">The Service to serve a Scope as a resource server.</param>
/// <param name="stopServingScopeService">The Service to stop serving a Scope.</param>
/// <param name="updateClientService">The Service to update the Client.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/clients")]
[Tags("Clients")]
public sealed class ClientsController(
    AddRedirectUriService addRedirectUriService,
    AllowScopeService allowScopeService,
    CreateClientSecretService createClientSecretService,
    DeleteClientService deleteClientService,
    DisableClientService disableClientService,
    DisallowScopeService disallowScopeService,
    EnableClientService enableClientService,
    GetAllClientsService getAllClientsService,
    GetClientService getClientService,
    RegisterClientService registerClientService,
    RemoveRedirectUriService removeRedirectUriService,
    RevokeClientSecretService revokeClientSecretService,
    ServeScopeService serveScopeService,
    StopServingScopeService stopServingScopeService,
    UpdateClientService updateClientService) : ControllerBase
{
    /// <summary>
    ///     Registers a Client.
    /// </summary>
    /// <remarks>
    ///     A Confidential Client gets its first Secret, which is returned in plain text only in this response.
    ///     Public Clients have no Secret. The Type cannot be changed later.
    /// </remarks>
    /// <param name="request">The request to register the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id of the Client and, for a Confidential Client, its first Secret.</returns>
    /// <response code="201">Responds with the Id of the registered Client, its first Secret if it is Confidential, and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid, e.g. an invalid Redirect URI, Grant Type, TTL or unknown Scope.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<RegisterClientResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Create)]
    public async Task<IActionResult> Register(
        RegisterClientRequest request,
        CancellationToken cancellationToken)
    {
        var response = await registerClientService.ExecuteAsync(
            request,
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    /// <summary>
    ///     Deletes the Client based on the given Id.
    /// </summary>
    /// <remarks>
    ///     The Client is removed together with its Secrets and Redirect URIs. To keep it, disable it instead.
    ///     This operation is idempotent: deleting a Client that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the Client was deleted, or if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Delete)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await deleteClientService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Gets all Clients, ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all Clients.</returns>
    /// <response code="200">Responds with all Clients, or an empty list if no Clients exist.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GetAllClientsResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var clients = await getAllClientsService.ExecuteAsync(cancellationToken);

        return Ok(clients);
    }

    /// <summary>
    ///     Gets the Client based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Includes the Redirect URIs, the allowed Scopes and the metadata of all Secrets. Secrets themselves are never returned.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the Client, or 404 Not Found if the Client does not exist.</returns>
    /// <response code="200">Responds with the Client matching the given Id.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetClientResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Read)]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var client = await getClientService.ExecuteAsync(id, cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        return Ok(client);
    }

    /// <summary>
    ///     Updates the Client based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed,
    ///     fields that are null or missing are left unchanged. The Type cannot be changed.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="request">The request to update the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content if the Client was updated.</returns>
    /// <response code="204">Responds with no content once the Client was updated.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid, e.g. an invalid Grant Type or TTL.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        await updateClientService.ExecuteAsync(
            id,
            request,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Enables the Client based on the given Id.
    /// </summary>
    /// <remarks>
    ///     An enabled Client can obtain tokens. Enabling a Client that is already enabled also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Client is enabled.</returns>
    /// <response code="204">Responds with no content once the Client was enabled.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> Enable(
        Guid id,
        CancellationToken cancellationToken)
    {
        await enableClientService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Disables the Client based on the given Id.
    /// </summary>
    /// <remarks>
    ///     A disabled Client cannot obtain new tokens. Disabling a Client that is already disabled also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Client is disabled.</returns>
    /// <response code="204">Responds with no content once the Client was disabled.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> Disable(
        Guid id,
        CancellationToken cancellationToken)
    {
        await disableClientService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Registers a Redirect URI for the Client.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: registering a Redirect URI that is already registered also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="request">The request with the Redirect URI.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Redirect URI is registered.</returns>
    /// <response code="204">Responds with no content once the Redirect URI was registered.</response>
    /// <response code="400">Responds with problem details if the Redirect URI is not absolute, contains a fragment or does not use HTTPS.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/redirect-uris")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> AddRedirectUri(
        Guid id,
        AddRedirectUriRequest request,
        CancellationToken cancellationToken)
    {
        await addRedirectUriService.ExecuteAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes a Redirect URI from the Client.
    /// </summary>
    /// <remarks>
    ///     The URI is passed in the query string and compared exactly (case-sensitive).
    ///     This operation is idempotent: removing a Redirect URI that is not registered also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="uri" example="https://shop.example.com/callback">The Redirect URI to remove.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Redirect URI is removed.</returns>
    /// <response code="204">Responds with no content once the Redirect URI was removed, or if it was not registered.</response>
    /// <response code="400">Responds with validation problem details if the uri query parameter is missing.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/redirect-uris")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> RemoveRedirectUri(
        Guid id,
        [FromQuery, Required, StringLength(2048, MinimumLength = 1)] string uri,
        CancellationToken cancellationToken)
    {
        await removeRedirectUriService.ExecuteAsync(id, uri, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Allows the Client to request a Scope.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: allowing a Scope that is already allowed also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="scopeId" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Scope is allowed.</returns>
    /// <response code="204">Responds with no content once the Scope was allowed.</response>
    /// <response code="404">Responds with problem details if no Client or no Scope with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/scopes/{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> AllowScope(
        Guid id,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        await allowScopeService.ExecuteAsync(id, scopeId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Disallows the Client to request a Scope.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: disallowing a Scope that is not allowed also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="scopeId" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Scope is disallowed.</returns>
    /// <response code="204">Responds with no content once the Scope was disallowed, or if it was not allowed.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/scopes/{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> DisallowScope(
        Guid id,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        await disallowScopeService.ExecuteAsync(id, scopeId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Makes the Client serve a Scope as a resource server.
    /// </summary>
    /// <remarks>
    ///     The Client can then introspect Access Tokens of other Clients that contain this Scope, and sees only the
    ///     Scopes it serves. Only Confidential Clients can serve Scopes, and never System Scopes such as
    ///     <c>openid</c> or <c>authora.admin</c>, which only Authora serves.
    ///     This operation is idempotent: serving a Scope that is already served also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="scopeId" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Scope is served.</returns>
    /// <response code="204">Responds with no content once the Scope is served.</response>
    /// <response code="404">Responds with problem details if no Client or no Scope with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Client is a Public Client, which cannot introspect tokens, or the Scope is a System Scope, which only Authora serves.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/served-scopes/{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> ServeScope(
        Guid id,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        await serveScopeService.ExecuteAsync(id, scopeId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Stops the Client from serving a Scope as a resource server.
    /// </summary>
    /// <remarks>
    ///     This operation is idempotent: stopping a Scope that is not served also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="scopeId" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the Scope.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Scope is no longer served.</returns>
    /// <response code="204">Responds with no content once the Scope is no longer served, or if it was not served.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/served-scopes/{scopeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.Update)]
    public async Task<IActionResult> StopServingScope(
        Guid id,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        await stopServingScopeService.ExecuteAsync(id, scopeId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Creates a new Secret for the Client.
    /// </summary>
    /// <remarks>
    ///     Only Confidential Clients have Secrets. Existing Secrets stay active, so a Secret can be rotated
    ///     without downtime: create a new Secret, update the Client application, then revoke the old Secret.
    ///     The plain-text Secret is returned only in this response. The request body is optional.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="request">The optional request with the expiry of the Secret.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the new Secret.</returns>
    /// <response code="201">Responds with the new Secret, including its plain-text value.</response>
    /// <response code="400">Responds with problem details if the expiry is not in the future or more than 730 days from now.</response>
    /// <response code="404">Responds with problem details if no Client with the given Id exists.</response>
    /// <response code="409">Responds with problem details if the Client is a Public Client, which cannot have Secrets.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/secrets")]
    [Consumes("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<IssuedClientSecretResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.ManageSecrets)]
    public async Task<IActionResult> CreateSecret(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateClientSecretRequest? request,
        CancellationToken cancellationToken)
    {
        var secret = await createClientSecretService.ExecuteAsync(
            id,
            request ?? new CreateClientSecretRequest(ExpiresAt: null),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id }, secret);
    }

    /// <summary>
    ///     Revokes a Secret of the Client.
    /// </summary>
    /// <remarks>
    ///     A revoked Secret can no longer be used, but is kept for history.
    ///     This operation is idempotent: revoking an already revoked Secret also succeeds and keeps the original revocation time.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client.</param>
    /// <param name="secretId" example="0199d1a2-7c3e-7b4a-9f10-6c963f66afa6">Id of the Secret.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content once the Secret is revoked.</returns>
    /// <response code="204">Responds with no content once the Secret was revoked.</response>
    /// <response code="404">Responds with problem details if no Client or no Secret with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/secrets/{secretId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.Clients.ManageSecrets)]
    public async Task<IActionResult> RevokeSecret(
        Guid id,
        Guid secretId,
        CancellationToken cancellationToken)
    {
        await revokeClientSecretService.ExecuteAsync(id, secretId, cancellationToken);

        return NoContent();
    }
}
