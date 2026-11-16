// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Api.Authentication;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.Oidc.EndSession;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Authora.Api.Pages.OAuth;

/// <summary>
///     The end-session endpoint (OpenID Connect RP-Initiated Logout 1.0): a Client sends the browser here to sign the
///     User out of Authora, and gets it back at a registered Post-logout Redirect URI.
/// </summary>
/// <remarks>
///     <para>
///         The User is signed out without a question only if the <c>id_token_hint</c> was issued in the current
///         Session (same User, same sign-in). Otherwise the User must confirm, so another site cannot sign Users out
///         with a link. Without a Session, there is nothing to sign out.
///     </para>
///     <para>
///         Invalid requests are shown as an error and never redirected. The confirmation form needs an antiforgery
///         token; a POST without the form's fields is a logout request sent as a form, which is redirected to GET like
///         at the authorization endpoint, so the browser sends the <c>SameSite=Lax</c> Session cookie.
///     </para>
/// </remarks>
/// <param name="validateEndSessionRequestService">The Service to validate the request.</param>
/// <param name="signOutService">The Service to end the Session.</param>
/// <param name="antiforgery">Validates the antiforgery token of the confirmation form.</param>
[IgnoreAntiforgeryToken]
public sealed class EndSessionModel(
    ValidateEndSessionRequestService validateEndSessionRequestService,
    SignOutService signOutService,
    IAntiforgery antiforgery) : PageModel
{
    /// <summary>
    ///     Name of the confirmation form field with the pressed button. Only the confirmation form sends it.
    /// </summary>
    public const string DecisionField = "decision";

    /// <summary>
    ///     Value of the button that confirms signing out.
    /// </summary>
    public const string SignOutDecision = "sign-out";

    /// <summary>
    ///     The valid request to ask for confirmation, or null if no confirmation is shown.
    /// </summary>
    public ValidatedEndSessionRequest? ConfirmationRequest { get; private set; }

    /// <summary>
    ///     The request parameters, repeated as hidden fields of the confirmation form.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; private set; } = new Dictionary<string, string>();

    /// <summary>
    ///     The error of an invalid request, or null.
    /// </summary>
    public string? ErrorDescription { get; private set; }

    /// <summary>
    ///     Whether the User is signed out now (or was not signed in), shown when there is no Post-logout Redirect URI.
    /// </summary>
    public bool SignedOut { get; private set; }

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     Validates the logout request and signs the User out, or asks for confirmation.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the Post-logout Redirect URI, the confirmation, the result or the error.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var request = EndSessionRequest.FromParameters(name => Request.Query[name].ToArray());
        var result = await validateEndSessionRequestService.ExecuteAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            return Error(result.ErrorDescription);
        }

        var validated = result.Request;

        if (User.Identity?.IsAuthenticated != true)
        {
            return Finish(validated, signedOut: true);
        }

        if (validated.IsHintFor(GetUserId(), GetAuthenticatedAt()))
        {
            await SignOutAsync(validated, confirmedByUser: false, cancellationToken);

            return Finish(validated, signedOut: true);
        }

        ConfirmationRequest = validated;
        Parameters = GetParameters(request);

        return Page();
    }

    /// <summary>
    ///     Handles the answer of the User on the confirmation page. A POST without an answer is a logout request sent
    ///     with POST, which is redirected to the same request as GET.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the Post-logout Redirect URI, the result or the error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType || !Request.Form.TryGetValue(DecisionField, out var decision))
        {
            return RedirectToGet();
        }

        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        var request = EndSessionRequest.FromParameters(name => Request.Form[name].ToArray());
        var result = await validateEndSessionRequestService.ExecuteAsync(request, cancellationToken);

        if (!result.IsValid)
        {
            return Error(result.ErrorDescription);
        }

        var validated = result.Request;

        if (decision != SignOutDecision)
        {
            // Staying signed in still returns to the Client, which cannot tell whether the User signed out.
            return Finish(validated, signedOut: User.Identity?.IsAuthenticated != true);
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            await SignOutAsync(validated, confirmedByUser: true, cancellationToken);
        }

        return Finish(validated, signedOut: true);
    }

    private async Task SignOutAsync(ValidatedEndSessionRequest validated, bool confirmedByUser, CancellationToken cancellationToken)
    {
        await signOutService.ExecuteForClientAsync(SessionCookie.Read(Request), validated.Client, confirmedByUser, cancellationToken);
        SessionCookie.Delete(Response);
    }

    /// <summary>
    ///     Sends the browser to the Post-logout Redirect URI with the <c>state</c>, or shows the result.
    /// </summary>
    private IActionResult Finish(ValidatedEndSessionRequest validated, bool signedOut)
    {
        if (validated.PostLogoutRedirectUri is null)
        {
            SignedOut = signedOut;

            return Page();
        }

        Response.Headers.Location = validated.State is null
            ? validated.PostLogoutRedirectUri
            : QueryHelpers.AddQueryString(validated.PostLogoutRedirectUri, "state", validated.State);

        return StatusCode(StatusCodes.Status303SeeOther);
    }

    private PageResult Error(string errorDescription)
    {
        ErrorDescription = errorDescription;
        Response.StatusCode = StatusCodes.Status400BadRequest;

        return Page();
    }

    /// <summary>
    ///     Sends a logout request that came as POST to the same URL as GET, with all form fields as query parameters.
    /// </summary>
    private StatusCodeResult RedirectToGet()
    {
        var fields = Request.HasFormContentType ? Request.Form : FormCollection.Empty;

        Response.Headers.Location = $"{Request.PathBase}{Request.Path}{QueryString.Create(fields)}";

        return StatusCode(StatusCodes.Status303SeeOther);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }

    private DateTime GetAuthenticatedAt()
    {
        var value = User.FindFirst(SessionClaimTypes.AuthenticationTime)!.Value;

        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(value, CultureInfo.InvariantCulture)).UtcDateTime;
    }

    private static Dictionary<string, string> GetParameters(EndSessionRequest request)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["id_token_hint"] = request.IdTokenHint,
            ["client_id"] = request.ClientId,
            ["post_logout_redirect_uri"] = request.PostLogoutRedirectUri,
            ["state"] = request.State
        };

        return parameters
            .Where(parameter => parameter.Value is not null)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value!);
    }
}
