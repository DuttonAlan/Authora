// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

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
///     <para>
///         Consent policy: the User is asked on every authorization request, and decisions are not remembered.
///         Only first-party Clients, e.g. the admin interface, get the code without asking, unless they send
///         <c>prompt=consent</c> (TD-012). The consent form repeats the request parameters, so they are validated
///         again when it is submitted. The antiforgery token binds the form to the signed-in User.
///     </para>
///     <para>
///         OpenID Connect: <c>prompt=login</c>, <c>prompt=select_account</c> and an exceeded <c>max_age</c> send a
///         signed-in User to the sign-in page again. <c>prompt=none</c> never shows a page: a signed-in User gets the
///         code from a first-party Client, otherwise it is answered with <c>login_required</c> or
///         <c>consent_required</c>.
///     </para>
/// </remarks>
/// <param name="validateAuthorizationRequestService">The Service to validate the request.</param>
/// <param name="issueAuthorizationCodeService">The Service to issue the Authorization Code.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
/// <param name="timeProvider">The clock for <c>max_age</c>.</param>
public sealed class AuthorizeModel(
    ValidateAuthorizationRequestService validateAuthorizationRequestService,
    IssueAuthorizationCodeService issueAuthorizationCodeService,
    IOptions<OAuthOptions> options,
    TimeProvider timeProvider) : PageModel
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

        var validated = result.Request;

        // Validated before signing in, so Users are not asked to sign in for a request that would fail anyway.
        var mustSignIn = User.Identity?.IsAuthenticated != true
            || validated.RequiresSignIn(GetAuthenticatedAt(), timeProvider.GetUtcNow().UtcDateTime);

        if (validated.Prompts.Contains(PromptValues.None))
        {
            if (!mustSignIn && !validated.RequiresConsent)
            {
                return await IssueCodeAsync(validated, cancellationToken);
            }

            // Consent is asked on every request of a third-party Client, so a signed-in User still needs a page.
            return RedirectToClient(validated.RedirectUri, new Dictionary<string, string?>
            {
                ["error"] = mustSignIn ? OAuthErrors.LoginRequired : OAuthErrors.ConsentRequired,
                ["error_description"] = mustSignIn ? "The User must sign in." : "The User must consent.",
                ["state"] = validated.State
            });
        }

        if (mustSignIn)
        {
            return SignIn(request);
        }

        if (!validated.RequiresConsent)
        {
            return await IssueCodeAsync(validated, cancellationToken);
        }

        return ShowConsent(request, validated);
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
            return SignIn(request);
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

        // Checked again, because the consent screen may have been open for longer than max_age allows. Denying needs
        // no recent sign-in, so only approving is checked.
        if (validated.RequiresSignIn(GetAuthenticatedAt(), timeProvider.GetUtcNow().UtcDateTime))
        {
            return SignIn(request);
        }

        return await IssueCodeAsync(validated, cancellationToken);
    }

    /// <summary>
    ///     Issues the Authorization Code for the signed-in User and sends it to the Client.
    /// </summary>
    private async Task<StatusCodeResult> IssueCodeAsync(ValidatedAuthorizationRequest validated, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var code = await issueAuthorizationCodeService.ExecuteAsync(validated, userId, GetAuthenticatedAt(), cancellationToken);

        return RedirectToClient(validated.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = validated.State
        });
    }

    /// <summary>
    ///     Sends the User to the sign-in page, and from there back to the authorization request.
    /// </summary>
    /// <remarks>
    ///     The way back leaves out the values of <c>prompt</c> that require a new sign-in and <c>max_age</c>, which the
    ///     new sign-in satisfies, so the User is not sent to the sign-in page again and again. The ID Token always
    ///     contains <c>auth_time</c>, so the Client can still check them.
    /// </remarks>
    private ChallengeResult SignIn(AuthorizationRequest request)
    {
        var parameters = GetParameters(request);
        parameters.Remove("max_age");

        var prompts = (request.Prompt ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(prompt => prompt is not (PromptValues.Login or PromptValues.SelectAccount))
            .ToList();

        if (prompts.Count > 0)
        {
            parameters["prompt"] = string.Join(' ', prompts);
        }
        else
        {
            parameters.Remove("prompt");
        }

        var returnUrl = QueryHelpers.AddQueryString(
            $"{Request.PathBase}{Request.Path}",
            parameters.Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value)));

        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl });
    }

    /// <summary>
    ///     When the signed-in User signed in, from the Session.
    /// </summary>
    private DateTime GetAuthenticatedAt()
    {
        var value = User.FindFirst(SessionClaimTypes.AuthenticationTime)!.Value;

        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(value, CultureInfo.InvariantCulture)).UtcDateTime;
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
            ["response_mode"] = request.ResponseMode,
            ["nonce"] = request.Nonce,
            ["prompt"] = request.Prompt,
            ["max_age"] = request.MaxAge
        };

        return parameters
            .Where(parameter => parameter.Value is not null)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value!);
    }
}
