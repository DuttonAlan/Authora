// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Api.Authentication;
using Authora.Core.Application.Consents.Check;
using Authora.Core.Application.Consents.Remember;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Authorize;

using Microsoft.AspNetCore.Antiforgery;
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
///         Consent policy: approving a third-party Client is remembered as the User's Consent, so the User is only
///         asked again when the Client requests a Scope the Consent does not contain yet. First-party Clients, e.g.
///         the admin interface, get the code without asking. <c>prompt=consent</c> always shows the screen. The
///         consent form repeats the request parameters, so they are validated again when it is submitted. The
///         antiforgery token binds the form to the signed-in User.
///     </para>
///     <para>
///         OpenID Connect: <c>prompt=login</c>, <c>prompt=select_account</c> and an exceeded <c>max_age</c> send a
///         signed-in User to the sign-in page again. <c>prompt=none</c> never shows a page: a signed-in User gets the
///         code if no consent is needed, otherwise it is answered with <c>login_required</c> or
///         <c>consent_required</c>.
///     </para>
/// </remarks>
/// <param name="validateAuthorizationRequestService">The Service to validate the request.</param>
/// <param name="checkConsentService">The Service to check whether the User must be asked for consent.</param>
/// <param name="rememberConsentService">The Service to remember the approval of the User.</param>
/// <param name="issueAuthorizationCodeService">The Service to issue the Authorization Code.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
/// <param name="timeProvider">The clock for <c>max_age</c>.</param>
/// <param name="antiforgery">Validates the antiforgery token of the consent form.</param>
[IgnoreAntiforgeryToken]
public sealed class AuthorizeModel(
    ValidateAuthorizationRequestService validateAuthorizationRequestService,
    CheckConsentService checkConsentService,
    RememberConsentService rememberConsentService,
    IssueAuthorizationCodeService issueAuthorizationCodeService,
    IOptions<OAuthOptions> options,
    TimeProvider timeProvider,
    IAntiforgery antiforgery) : PageModel
{
    /// <summary>
    ///     Name of the consent form field with the pressed button. Only the consent form sends it.
    /// </summary>
    public const string DecisionField = "decision";

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

        // Without a sign-in the User is unknown, so the Consent is only checked once the User is back from signing in.
        var mustConsent = !mustSignIn
            && await checkConsentService.ExecuteAsync(validated, GetUserId(), cancellationToken) == ConsentCheckResult.Required;

        if (validated.Prompts.Contains(PromptValues.None))
        {
            if (!mustSignIn && !mustConsent)
            {
                return await IssueCodeAsync(validated, cancellationToken);
            }

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

        if (!mustConsent)
        {
            return await IssueCodeAsync(validated, cancellationToken);
        }

        return ShowConsent(request, validated);
    }

    /// <summary>
    ///     Handles the decision of the User on the consent screen: issues an Authorization Code or sends
    ///     <c>access_denied</c> to the Client. A POST without a decision is an authorization request sent with POST
    ///     (OpenID Connect Core 1.0, Section 3.1.2.1), which is redirected to the same request as GET.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the Client, a redirect to the sign-in page, or the error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType || !Request.Form.TryGetValue(DecisionField, out var decision))
        {
            return RedirectToGet();
        }

        // The page ignores antiforgery tokens, so that Clients can POST authorization requests; consent needs one.
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

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

        // Remembered before the code is issued, so the code of a third-party Client is bound to the Consent.
        await rememberConsentService.ExecuteAsync(validated, GetUserId(), cancellationToken);

        return await IssueCodeAsync(validated, cancellationToken);
    }

    /// <summary>
    ///     Sends an authorization request that came as POST to the same URL as GET, with all form fields as query
    ///     parameters, including unknown and repeated ones, so it is validated exactly like a GET request.
    /// </summary>
    /// <remarks>
    ///     A Client's form POST is a cross-site request, so the browser does not send the <c>SameSite=Lax</c> Session
    ///     cookie with it, and a signed-in User would have to sign in again, or get <c>login_required</c> for
    ///     <c>prompt=none</c>. The GET that follows the redirect is a top-level navigation and carries the cookie.
    /// </remarks>
    private StatusCodeResult RedirectToGet()
    {
        var fields = Request.HasFormContentType ? Request.Form : FormCollection.Empty;
        var query = QueryString.Create(fields);

        Response.Headers.Location = $"{Request.PathBase}{Request.Path}{query}";

        return StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>
    ///     Issues the Authorization Code for the signed-in User and sends it to the Client.
    /// </summary>
    private async Task<StatusCodeResult> IssueCodeAsync(ValidatedAuthorizationRequest validated, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);
        var code = await issueAuthorizationCodeService.ExecuteAsync(validated, GetUserId(), sessionId, GetAuthenticatedAt(), cancellationToken);

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
    ///     The Id of the signed-in User, from the Session.
    /// </summary>
    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
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
