// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The Service to validate a request to the /authorize endpoint.
/// </summary>
/// <remarks>
///     The Client and the Redirect URI are validated first. Until both are valid, errors are only shown to the User.
///     After that, errors are sent to the Redirect URI. The request is validated again when the User submits the
///     consent form, because the form fields come from the browser.
/// </remarks>
/// <param name="clientRepository">An Instance of a IClientRepository to load the Client.</param>
/// <param name="logger">The Logger to record rejected requests.</param>
public sealed class ValidateAuthorizationRequestService(
    IClientRepository clientRepository,
    ILogger<ValidateAuthorizationRequestService> logger)
{
    /// <summary>
    ///     The only supported <c>response_type</c>.
    /// </summary>
    public const string ResponseTypeCode = "code";

    /// <summary>
    ///     The only supported <c>response_mode</c>.
    /// </summary>
    public const string ResponseModeQuery = "query";

    /// <summary>
    ///     Validates an authorization request.
    /// </summary>
    /// <param name="request">The parameters of the request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The valid request, or the error and whether it may be redirected to the Client.</returns>
    public async Task<AuthorizationRequestValidationResult> ExecuteAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await ValidateAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            logger.AuthorizationRequestRejected(request.ClientId ?? "(none)", result.Error);
        }

        return result;
    }

    private async Task<AuthorizationRequestValidationResult> ValidateAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DuplicateParameters.Contains("client_id") || request.DuplicateParameters.Contains("redirect_uri"))
        {
            return AuthorizationRequestValidationResult.NotRedirectable(
                OAuthErrors.InvalidRequest,
                "The client_id and redirect_uri parameters must not be sent more than once.");
        }

        if (!Guid.TryParseExact(request.ClientId, "D", out var clientId))
        {
            return AuthorizationRequestValidationResult.NotRedirectable(
                OAuthErrors.InvalidRequest,
                "The client_id parameter is missing or malformed.");
        }

        var client = await clientRepository.GetClientById(clientId, cancellationToken);

        if (client is null)
        {
            return AuthorizationRequestValidationResult.NotRedirectable(
                OAuthErrors.InvalidRequest,
                "The Client is unknown.");
        }

        string redirectUri;

        if (request.RedirectUri is null)
        {
            // OAuth 2.1, Section 4.1.1: redirect_uri may only be omitted if exactly one Redirect URI is registered.
            if (client.RedirectUris.Count != 1)
            {
                return AuthorizationRequestValidationResult.NotRedirectable(
                    OAuthErrors.InvalidRequest,
                    "The redirect_uri parameter is required.");
            }

            redirectUri = client.RedirectUris.Single().Value;
        }
        else if (client.IsRedirectUriAllowed(request.RedirectUri))
        {
            redirectUri = request.RedirectUri;
        }
        else
        {
            return AuthorizationRequestValidationResult.NotRedirectable(
                OAuthErrors.InvalidRequest,
                "The redirect_uri is not registered for the Client.");
        }

        if (client.Status == ClientStatus.Disabled)
        {
            return AuthorizationRequestValidationResult.NotRedirectable(
                OAuthErrors.UnauthorizedClient,
                "The Client is disabled.");
        }

        // From here on, the Client and the Redirect URI are valid, so errors are sent to the Client.
        var state = request.DuplicateParameters.Contains("state") ? null : request.State;

        AuthorizationRequestValidationResult Fail(string error, string description)
        {
            return AuthorizationRequestValidationResult.Redirectable(error, description, redirectUri, state);
        }

        if (request.DuplicateParameters.Count > 0)
        {
            return Fail(
                OAuthErrors.InvalidRequest,
                $"The parameter {request.DuplicateParameters.First()} must not be sent more than once.");
        }

        if (request.ResponseType is null)
        {
            return Fail(OAuthErrors.InvalidRequest, "The response_type parameter is required.");
        }

        if (request.ResponseType != ResponseTypeCode)
        {
            return Fail(OAuthErrors.UnsupportedResponseType, "Only the response_type code is supported.");
        }

        if (request.ResponseMode is not null && request.ResponseMode != ResponseModeQuery)
        {
            return Fail(OAuthErrors.InvalidRequest, "Only the response_mode query is supported.");
        }

        if (!client.IsGrantTypeAllowed(GrantTypes.AuthorizationCode))
        {
            return Fail(OAuthErrors.UnauthorizedClient, "The Client is not allowed to use the Authorization Code Grant.");
        }

        if (request.CodeChallenge is null)
        {
            return Fail(OAuthErrors.InvalidRequest, "PKCE is required: the code_challenge parameter is missing.");
        }

        // Without a method, RFC 7636 defaults to plain, which is not allowed.
        if (request.CodeChallengeMethod != Pkce.MethodS256)
        {
            return Fail(OAuthErrors.InvalidRequest, "The code_challenge_method must be S256.");
        }

        if (!Pkce.IsValidCodeChallenge(request.CodeChallenge))
        {
            return Fail(OAuthErrors.InvalidRequest, "The code_challenge must be a Base64Url encoded SHA-256 hash.");
        }

        if (!ScopeList.TryParse(request.Scope, out var scopeNames))
        {
            return Fail(OAuthErrors.InvalidScope, $"The scope parameter is missing, contains an invalid Scope or is longer than {ScopeList.MaxLength} characters.");
        }

        var notAllowed = scopeNames.FirstOrDefault(name => !client.IsScopeAllowed(name));

        if (notAllowed is not null)
        {
            return Fail(OAuthErrors.InvalidScope, $"The Scope {notAllowed} is not allowed for the Client.");
        }

        var scopes = scopeNames
            .Select(name => client.AllowedScopes.First(scope => string.Equals(scope.Name, name, StringComparison.Ordinal)))
            .ToList();

        return AuthorizationRequestValidationResult.Valid(new ValidatedAuthorizationRequest(
            client,
            redirectUri,
            request.RedirectUri,
            scopes,
            state,
            request.CodeChallenge));
    }
}
