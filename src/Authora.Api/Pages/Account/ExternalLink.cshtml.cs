// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.ExternalSignIn.Start;
using Authora.Core.Domain.Entities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     Starts linking an account at an external identity provider to the signed-in User: the User is sent to the
///     provider, and its response arrives at <see cref="ExternalCallbackModel"/>, which creates the link.
/// </summary>
/// <remarks>
///     Requires a recent authentication, so a browser left signed in cannot be used to add a way to sign in. The flow
///     is bound to the current Session: the response is only accepted in it.
/// </remarks>
/// <param name="startService">The Service to create the authorization request.</param>
/// <param name="flowCookie">The cookie that keeps the flow until the response arrives.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class ExternalLinkModel(StartExternalSignInService startService, ExternalSignInCookie flowCookie) : PageModel
{
    /// <summary>
    ///     The Slug of the provider from the URL.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    ///     The error to show.
    /// </summary>
    public string ErrorMessage { get; private set; } = ExternalSignInModel.UnknownProviderMessage;

    /// <summary>
    ///     Gets the path that starts linking an account at a provider.
    /// </summary>
    /// <param name="slug">The Slug of the provider.</param>
    /// <returns>The path.</returns>
    public static string GetPath(string slug) => $"{ExternalAccountsModel.PagePath}/link/{Uri.EscapeDataString(slug)}";

    /// <summary>
    ///     Sends the User to the provider, or shows why that is not possible.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the provider, or the page with an error.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IdentityProvider.IsValidSlug(Slug))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;

            return Page();
        }

        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        var result = await startService.ExecuteAsync(
            new StartExternalSignInRequest(Slug, ExternalAccountsModel.PagePath, new ExternalLinkRequest(userId, sessionId)),
            cancellationToken);

        switch (result.Status)
        {
            case StartExternalSignInStatus.Started:
                flowCookie.Append(Response, result.Flow!);

                return Redirect(result.AuthorizationUrl!.AbsoluteUri);

            case StartExternalSignInStatus.AlreadyLinked:
                return LocalRedirect($"{ExternalAccountsModel.PagePath}?status=already-linked");

            case StartExternalSignInStatus.SessionEnded:
                SessionCookie.Delete(Response);

                return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{ExternalAccountsModel.PagePath}")}");

            case StartExternalSignInStatus.ProviderUnavailable:
                ErrorMessage = $"{result.ProviderName} cannot be reached right now. Please try again later.";
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                return Page();

            default:
                Response.StatusCode = StatusCodes.Status404NotFound;

                return Page();
        }
    }
}
