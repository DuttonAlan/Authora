// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.ExternalSignIn.Start;
using Authora.Core.Domain.Entities;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     Starts a sign-in through an external identity provider: the buttons of the sign-in page link here, and the
///     User is sent on to the provider. Its response arrives at <see cref="ExternalCallbackModel"/>.
/// </summary>
/// <remarks>
///     The buttons are links, not forms, so the Content Security Policy does not have to allow forms to lead to the
///     provider. Starting only sets the encrypted cookie of the flow (<see cref="ExternalSignInCookie"/>); the
///     Session of the browser stays as it is until the response arrives.
/// </remarks>
/// <param name="startService">The Service to create the authorization request.</param>
/// <param name="flowCookie">The cookie that keeps the flow until the response arrives.</param>
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class ExternalSignInModel(StartExternalSignInService startService, ExternalSignInCookie flowCookie) : PageModel
{
    /// <summary>
    ///     The message when the provider is unknown or disabled.
    /// </summary>
    public const string UnknownProviderMessage = "This sign-in option is not available.";

    /// <summary>
    ///     The Slug of the provider from the URL.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    ///     The local URL to return to after signing in. Other URLs are ignored, so the page is not an open redirect.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The error to show.
    /// </summary>
    public string ErrorMessage { get; private set; } = UnknownProviderMessage;

    /// <summary>
    ///     The sign-in page, with the way back to the <see cref="ReturnUrl"/>.
    /// </summary>
    public string SignInUrl => $"{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl)}";

    private string SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : SessionAuthenticationHandler.SignInPath;

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

        var result = await startService.ExecuteAsync(new StartExternalSignInRequest(Slug, SafeReturnUrl), cancellationToken);

        switch (result.Status)
        {
            case StartExternalSignInStatus.Started:
                flowCookie.Append(Response, result.Flow!);

                return Redirect(result.AuthorizationUrl!.AbsoluteUri);

            case StartExternalSignInStatus.ProviderUnavailable:
                ErrorMessage = $"{result.ProviderName} cannot be reached right now. Please try again later or sign in with your password.";
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                return Page();

            default:
                Response.StatusCode = StatusCodes.Status404NotFound;

                return Page();
        }
    }
}
