// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Authorize;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.OAuth;

/// <summary>
///     The authorization endpoint (RFC 6749, Section 3.1). Validates the authorization request, makes sure the User
///     is signed in, asks for consent and sends the Authorization Code or the error back to the Client.
/// </summary>
/// <remarks>
///     Consent policy: the User is asked on every authorization request, and decisions are not remembered.
///     The consent form repeats the request parameters, so they are validated again when it is submitted.
///     The antiforgery token binds the form to the signed-in User.
/// </remarks>
/// <param name="validateAuthorizationRequestService">The Service to validate the request.</param>
/// <param name="issueAuthorizationCodeService">The Service to issue the Authorization Code.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
public sealed class AuthorizeModel(
    ValidateAuthorizationRequestService validateAuthorizationRequestService,
    IssueAuthorizationCodeService issueAuthorizationCodeService,
    IOptions<OAuthOptions> options) : PageModel
{
    /// <summary>
    ///     Value of the decision button to approve the request.
    /// </summary>
    public const string AllowDecision = "allow";

    /// <summary>
    ///     The valid request to ask consent for, or null if an error is shown.
    /// </summary>
    public ValidatedAuthorizationRequest? ConsentRequest { get; private set; }

    /// <summary>
    ///     The request parameters, repeated as hidden fields of the consent form.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; private set; } = new Dictionary<string, string>();

    /// <summary>
    ///     The error that cannot be sent to the Client and is shown to the User instead, or null.
    /// </summary>
    public string? ErrorDescription { get; private set; }

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     The origin of the Redirect URI, shown so the User sees where they will be sent.
    /// </summary>
    public string? RedirectOrigin => ConsentRequest is null
        ? null
        : new Uri(ConsentRequest.RedirectUri).GetLeftPart(UriPartial.Authority);

    /// <summary>
    ///     Validates the authorization request and shows the consent screen.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The consent screen, a redirect to the sign-in page, or the error.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var request = AuthorizationRequest.FromParameters(name => Request.Query[name].ToArray());
        var result = await validateAuthorizationRequestService.ExecuteAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            return Error(result);
        }

        // Validate before signing in, so Users are not asked to sign in for a request that would fail anyway.
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        return ShowConsent(request, result.Request);
    }

    /// <summary>
    ///     Handles the decision of the User: issues an Authorization Code or sends <c>access_denied</c> to the Client.
    /// </summary>
    /// <param name="decision">The pressed button: <see cref="AllowDecision"/> or anything else to deny.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the Client, a redirect to the sign-in page, or the error.</returns>
    public async Task<IActionResult> OnPostAsync(string? decision, CancellationToken cancellationToken)
    {
        var request = AuthorizationRequest.FromParameters(name => Request.Form[name].ToArray());
        var result = await validateAuthorizationRequestService.ExecuteAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            return Error(result);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            // Start over from the GET request, so the User sees the consent screen again after signing in.
            return Challenge(new AuthenticationProperties { RedirectUri = BuildAuthorizeUrl(request) });
        }

        var validated = result.Request;

        if (decision != AllowDecision)
        {
            return RedirectToClient(validated.RedirectUri, new Dictionary<string, string?>
            {
                ["error"] = OAuthErrors.AccessDenied,
                ["error_description"] = "The User denied the request.",
                ["state"] = validated.State
            });
        }

        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var code = await issueAuthorizationCodeService.ExecuteAsync(validated, userId, cancellationToken);

        return RedirectToClient(validated.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = validated.State
        });
    }

    private PageResult ShowConsent(AuthorizationRequest request, ValidatedAuthorizationRequest validated)
    {
        ConsentRequest = validated;
        Parameters = GetParameters(request);

        return Page();
    }

    private IActionResult Error(AuthorizationRequestValidationResult result)
    {
        if (result.ErrorRedirectUri is null)
        {
            // The Client or Redirect URI is invalid, so the error must not be sent anywhere (RFC 6749, Section 4.1.2.1).
            ErrorDescription = result.ErrorDescription;
            Response.StatusCode = StatusCodes.Status400BadRequest;

            return Page();
        }

        return RedirectToClient(result.ErrorRedirectUri, new Dictionary<string, string?>
        {
            ["error"] = result.Error,
            ["error_description"] = result.ErrorDescription,
            ["state"] = result.State
        });
    }

    /// <summary>
    ///     Sends the response to the validated Redirect URI. Adds <c>iss</c>, so the Client can detect mix-up attacks
    ///     (RFC 9207). Uses 303 See Other, so the browser does not repeat a POST at the Client (OAuth 2.1, Section 7.5.2).
    /// </summary>
    private StatusCodeResult RedirectToClient(string redirectUri, Dictionary<string, string?> parameters)
    {
        parameters["iss"] = options.Value.Issuer;

        var location = QueryHelpers.AddQueryString(
            redirectUri,
            parameters.Where(parameter => parameter.Value is not null));

        Response.Headers.Location = location;

        return StatusCode(StatusCodes.Status303SeeOther);
    }

    private string BuildAuthorizeUrl(AuthorizationRequest request)
    {
        return QueryHelpers.AddQueryString(
            $"{Request.PathBase}{Request.Path}",
            GetParameters(request).Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value)));
    }

    private static Dictionary<string, string> GetParameters(AuthorizationRequest request)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["response_type"] = request.ResponseType,
            ["scope"] = request.Scope,
            ["state"] = request.State,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = request.CodeChallengeMethod,
            ["response_mode"] = request.ResponseMode
        };

        return parameters
            .Where(parameter => parameter.Value is not null)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value!);
    }
}
