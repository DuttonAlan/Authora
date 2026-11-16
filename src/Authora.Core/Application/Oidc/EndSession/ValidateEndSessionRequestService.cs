// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     The Service to validate a request to the end-session endpoint (OpenID Connect RP-Initiated Logout 1.0).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>An <c>id_token_hint</c> must be an ID Token Authora issued; it may be expired.</item>
///         <item>A <c>client_id</c> must name an existing Client, and the Client of the hint if both are sent.</item>
///         <item>
///             A <c>post_logout_redirect_uri</c> needs a Client from <c>client_id</c> or the hint, which must be enabled
///             and have registered exactly that URI.
///         </item>
///     </list>
///     Whether the User is asked to confirm is decided with the current Session, see
///     <see cref="ValidatedEndSessionRequest.IsHintFor(Guid, DateTime)"/>.
/// </remarks>
/// <param name="clientRepository">An Instance of a IClientRepository to load the Client.</param>
/// <param name="idTokenHintReader">Reads and checks the <c>id_token_hint</c>.</param>
/// <param name="logger">The Logger to record rejected requests.</param>
public sealed class ValidateEndSessionRequestService(
    IClientRepository clientRepository,
    IdTokenHintReader idTokenHintReader,
    ILogger<ValidateEndSessionRequestService> logger)
{
    /// <summary>
    ///     Validates a logout request.
    /// </summary>
    /// <param name="request">The parameters of the request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The valid request, or the error to show.</returns>
    public async Task<EndSessionRequestValidationResult> ExecuteAsync(
        EndSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await ValidateAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            logger.EndSessionRequestRejected(request.ClientId ?? "(none)", result.ErrorDescription);
        }

        return result;
    }

    private async Task<EndSessionRequestValidationResult> ValidateAsync(
        EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DuplicateParameters.Count > 0)
        {
            return EndSessionRequestValidationResult.Invalid(
                $"The parameter {request.DuplicateParameters.First()} must not be sent more than once.");
        }

        IdTokenHint? hint = null;

        if (request.IdTokenHint is not null && !idTokenHintReader.TryRead(request.IdTokenHint, out hint))
        {
            return EndSessionRequestValidationResult.Invalid("The id_token_hint is not an ID Token issued by Authora.");
        }

        Client? client = null;

        if (request.ClientId is not null)
        {
            if (!Guid.TryParseExact(request.ClientId, "D", out var clientId))
            {
                return EndSessionRequestValidationResult.Invalid("The client_id is malformed.");
            }

            if (hint is not null && hint.ClientId != clientId)
            {
                return EndSessionRequestValidationResult.Invalid("The client_id does not match the id_token_hint.");
            }

            client = await clientRepository.GetClientById(clientId, cancellationToken);

            if (client is null)
            {
                return EndSessionRequestValidationResult.Invalid("The Client is unknown.");
            }
        }
        else if (hint is not null)
        {
            // The Client of an old hint may have been deleted since; the hint still identifies the User.
            client = await clientRepository.GetClientById(hint.ClientId, cancellationToken);
        }

        if (request.PostLogoutRedirectUri is not null)
        {
            if (client is null)
            {
                return EndSessionRequestValidationResult.Invalid(
                    "A post_logout_redirect_uri needs a client_id or an id_token_hint of an existing Client.");
            }

            if (client.Status == ClientStatus.Disabled)
            {
                return EndSessionRequestValidationResult.Invalid("The Client is disabled.");
            }

            if (!client.IsPostLogoutRedirectUriAllowed(request.PostLogoutRedirectUri))
            {
                return EndSessionRequestValidationResult.Invalid(
                    "The post_logout_redirect_uri is not registered for the Client.");
            }
        }

        return EndSessionRequestValidationResult.Valid(new ValidatedEndSessionRequest(
            client,
            request.PostLogoutRedirectUri,
            request.State,
            hint));
    }
}
