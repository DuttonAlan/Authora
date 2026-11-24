// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.ExternalSignIn;
using Authora.Core.Application.IdentityProviders;
using Authora.Core.Application.IdentityProviders.Create;
using Authora.Core.Application.IdentityProviders.Delete;
using Authora.Core.Application.IdentityProviders.Get;
using Authora.Core.Application.IdentityProviders.GetAll;
using Authora.Core.Application.IdentityProviders.SetEnabled;
using Authora.Core.Application.IdentityProviders.SetSecret;
using Authora.Core.Application.IdentityProviders.Update;
using Authora.Core.Application.OAuth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the Endpoints to manage external OpenID Connect identity providers, through which Users sign in.
/// </summary>
/// <param name="createService">The Service to create a provider.</param>
/// <param name="deleteService">The Service to delete a provider.</param>
/// <param name="getAllService">The Service to get all providers.</param>
/// <param name="getService">The Service to get a provider.</param>
/// <param name="setEnabledService">The Service to enable or disable a provider.</param>
/// <param name="setSecretService">The Service to set or remove the Client Secret of a provider.</param>
/// <param name="updateService">The Service to update a provider.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer of Authora, for the Redirect URI.</param>
[ApiController]
[Authorize(AuthorizationPolicies.AdminApi)]
[Route("api/identity-providers")]
[Tags("Identity providers")]
public sealed class IdentityProvidersController(
    CreateIdentityProviderService createService,
    DeleteIdentityProviderService deleteService,
    GetAllIdentityProvidersService getAllService,
    GetIdentityProviderService getService,
    SetIdentityProviderEnabledService setEnabledService,
    SetIdentityProviderSecretService setSecretService,
    UpdateIdentityProviderService updateService,
    IOptions<OAuthOptions> oauthOptions) : ControllerBase
{
    /// <summary>
    ///     Creates an identity provider.
    /// </summary>
    /// <remarks>
    ///     The discovery document of the Issuer (<c>{Issuer}/.well-known/openid-configuration</c>) is fetched once, so an
    ///     Issuer that cannot be used is rejected right away. The Client Secret is stored encrypted and never returned.
    ///     Register the returned Redirect URI at the provider.
    /// </remarks>
    /// <param name="request">The request to create the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>201 Created with the Id and the Redirect URI of the provider.</returns>
    /// <response code="201">Responds with the Id and the Redirect URI of the created provider, and its location in the Location header.</response>
    /// <response code="400">Responds with validation problem details if the request is invalid or the discovery document of the Issuer cannot be used.</response>
    /// <response code="409">Responds with problem details if the Slug is already used.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CreateIdentityProviderResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> Create(CreateIdentityProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = await createService.ExecuteAsync(request, cancellationToken);
        var response = new CreateIdentityProviderResponse(
            id,
            ExternalSignInOptions.GetRedirectUri(oauthOptions.Value.Issuer, request.Slug.Trim()));

        return CreatedAtAction(nameof(Get), new { id }, response);
    }

    /// <summary>
    ///     Gets all identity providers, in the order of the sign-in page.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with all providers.</returns>
    /// <response code="200">Responds with all providers, or an empty list. Client Secrets are never returned.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IdentityProviderResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await getAllService.ExecuteAsync(cancellationToken));
    }

    /// <summary>
    ///     Gets the identity provider based on the given Id.
    /// </summary>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the provider, or 404 Not Found.</returns>
    /// <response code="200">Responds with the provider. The Client Secret is never returned.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<IdentityProviderResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var provider = await getService.ExecuteAsync(id, cancellationToken);

        return provider is null ? NotFound() : Ok(provider);
    }

    /// <summary>
    ///     Updates the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     This is a partial update: only fields that are set in the request are changed. The Slug and the Preset cannot
    ///     be changed, the Issuer of the Google preset neither. A new Issuer is checked by fetching its discovery document
    ///     once. The Client Secret has an endpoint of its own.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="request">The request to update the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the provider was updated.</response>
    /// <response code="400">Responds with validation problem details if the request is invalid or the discovery document of a new Issuer cannot be used.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateIdentityProviderRequest request, CancellationToken cancellationToken)
    {
        await updateService.ExecuteAsync(id, request, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Deletes the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     A provider through which accounts are linked cannot be deleted: disable it instead, or have the Users remove
    ///     their links first. This operation is idempotent: deleting a provider that does not exist also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the provider was deleted, or if it does not exist.</response>
    /// <response code="409">Responds with problem details if accounts are linked through the provider.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await deleteService.ExecuteAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Enables the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     An enabled provider is shown on the sign-in page. Enabling a provider that is already enabled also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the provider was enabled.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
    {
        await setEnabledService.ExecuteAsync(id, enabled: true, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Disables the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     A disabled provider disappears from the sign-in page, and sign-ins through it are rejected, also those already
    ///     running. Linked accounts are kept. Disabling a provider that is already disabled also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the provider was disabled.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        await setEnabledService.ExecuteAsync(id, enabled: false, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Sets the Client Secret of the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Replaces the current secret. It is stored encrypted with ASP.NET Core Data Protection and never returned;
    ///     the provider only shows whether one is set.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="request">The new Client Secret.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the secret was saved.</response>
    /// <response code="400">Responds with validation problem details if the request body is invalid.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPut("{id:guid}/secret")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> SetSecret(Guid id, SetIdentityProviderSecretRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await setSecretService.ExecuteAsync(id, request.ClientSecret, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Removes the Client Secret of the identity provider based on the given Id.
    /// </summary>
    /// <remarks>
    ///     Authora then authenticates at the provider as a Public Client, with PKCE only. Removing a secret that is not
    ///     set also succeeds.
    /// </remarks>
    /// <param name="id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the secret was removed.</response>
    /// <response code="404">Responds with problem details if no provider with the given Id exists.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpDelete("{id:guid}/secret")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [Authorize(PermissionNames.IdentityProviders.Manage)]
    public async Task<IActionResult> RemoveSecret(Guid id, CancellationToken cancellationToken)
    {
        await setSecretService.ExecuteAsync(id, clientSecret: null, cancellationToken);

        return NoContent();
    }
}
