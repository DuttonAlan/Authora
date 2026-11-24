// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.ExternalSignIn.Complete;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The Redirect URI of Authora for external identity providers: receives the authorization response, and signs
///     the User in, links the account, or shows why that failed.
/// </summary>
/// <remarks>
///     <para>
///         The cookie of the flow is read and deleted on every response, so a flow is completed at most once. After a
///         sign-in, the authorization request of a Client continues exactly as after a password sign-in.
///     </para>
///     <para>
///         A User who enrolled a second factor gets a pending sign-in. Its cookie is <c>SameSite=Strict</c>, which
///         browsers do not send on a redirect that started at the provider's site, so the page continues to the second
///         factor with a navigation of its own (a refresh and a link) instead of a redirect.
///     </para>
/// </remarks>
/// <param name="completeService">The Service to check the response and sign in or link.</param>
/// <param name="signOutService">The Service to end the previous Session of the browser.</param>
/// <param name="flowCookie">The cookie of the running flow.</param>
/// <param name="signInOptions">The sign-in settings with the lifetime of a pending sign-in.</param>
[EnableRateLimiting(RateLimitPolicies.ExternalSignIn)]
public sealed class ExternalCallbackModel(
    CompleteExternalSignInService completeService,
    SignOutService signOutService,
    ExternalSignInCookie flowCookie,
    IOptions<SignInOptions> signInOptions) : PageModel
{
    /// <summary>
    ///     The message for a failed sign-in. The reason is only in the logs and the audit log.
    /// </summary>
    public const string FailedMessage = "The sign-in did not work. Please try again.";

    /// <summary>
    ///     The message for an account at the provider that is not linked to a User.
    /// </summary>
    public const string NotLinkedMessage = "This account is not linked. Sign in with your password to link it.";

    /// <summary>
    ///     The message for an account at the provider that is linked to another User.
    /// </summary>
    public const string LinkConflictMessage = "This account cannot be linked: it is already linked to another account, or yours is already linked to another account there.";

    /// <summary>
    ///     The Slug of the provider from the URL.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    ///     The outcome to show, if the page is shown.
    /// </summary>
    public ExternalSignInStatus Status { get; private set; } = ExternalSignInStatus.Failed;

    /// <summary>
    ///     The Name of the provider, or null if it is unknown.
    /// </summary>
    public string? ProviderName { get; private set; }

    /// <summary>
    ///     Where "Continue" leads: the second factor of a pending sign-in, or the sign-in page.
    /// </summary>
    public string ContinueUrl { get; private set; } = SessionAuthenticationHandler.SignInPath;

    /// <summary>
    ///     Handles the authorization response.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to continue, or the page.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var flow = flowCookie.Take(HttpContext);
        var query = Request.Query;

        var result = await completeService.ExecuteAsync(
            new CompleteExternalSignInRequest(
                flow,
                Slug,
                query["state"].FirstOrDefault(),
                query["code"].FirstOrDefault(),
                query["error"].FirstOrDefault(),
                query["iss"].FirstOrDefault()),
            GetCurrentSessionId(),
            cancellationToken);

        Status = result.Status;
        ProviderName = result.ProviderName;

        var returnUrl = !string.IsNullOrEmpty(result.ReturnUrl) && Url.IsLocalUrl(result.ReturnUrl)
            ? result.ReturnUrl
            : SessionAuthenticationHandler.SignInPath;

        switch (result.Status)
        {
            case ExternalSignInStatus.SignedIn:
                // Like on the sign-in page: a new Session token, and the Session of the previous User of this browser ends.
                await signOutService.ExecuteAsync(SessionCookie.Read(Request), cancellationToken);
                SessionCookie.Append(Response, result.SessionToken!);
                PendingSignInCookie.Delete(Response);

                return result.MustEnrollSecondFactor
                    ? LocalRedirect($"{AuthenticatorModel.PagePath}?returnUrl={Uri.EscapeDataString(returnUrl)}")
                    : LocalRedirect(returnUrl);

            case ExternalSignInStatus.Pending:
                PendingSignInCookie.Append(Response, result.ChallengeToken!, signInOptions.Value.PendingSignInLifetime);
                ContinueUrl = $"{SecondFactorModel.PagePath}?returnUrl={Uri.EscapeDataString(returnUrl)}";

                return Page();

            case ExternalSignInStatus.Linked:
                return LocalRedirect($"{ExternalAccountsModel.PagePath}?status=linked");

            case ExternalSignInStatus.AlreadyLinked:
                return LocalRedirect($"{ExternalAccountsModel.PagePath}?status=already-linked");

            case ExternalSignInStatus.NotLinked:
                ContinueUrl = $"{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";

                return Page();

            case ExternalSignInStatus.LinkConflict:
                ContinueUrl = ExternalAccountsModel.PagePath;

                return Page();

            default:
                ContinueUrl = flow?.IsLink == true
                    ? ExternalAccountsModel.PagePath
                    : $"{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";

                return Page();
        }
    }

    private Guid? GetCurrentSessionId()
    {
        return User.Identity?.IsAuthenticated == true
            && Guid.TryParse(User.FindFirst(SessionClaimTypes.SessionId)?.Value, out var sessionId)
                ? sessionId
                : null;
    }
}
