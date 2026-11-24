// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.ExternalSignIn.CompleteLink;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     Where the browser returns after signing in to link an account at an external identity provider that has the
///     Email of the User (see <see cref="ExternalCallbackModel"/>): links it, then continues, e.g. with the
///     authorization request of a Client.
/// </summary>
/// <remarks>
///     Only links if the sign-in happened after the account arrived and is one of exactly that User. The cookie of the
///     pending link is deleted on every outcome except <see cref="CompletePendingLinkResult.SignInRequired"/>, so
///     abandoning or failing the sign-in links nothing later.
/// </remarks>
/// <param name="completeLinkService">The Service that links the account.</param>
/// <param name="linkCookie">The cookie of the pending link.</param>
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExternalSignIn)]
public sealed class ExternalLinkCompleteModel(CompletePendingLinkService completeLinkService, ExternalLinkCookie linkCookie) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/external-accounts/complete";

    /// <summary>
    ///     The message when there is no pending link, it expired, or it could not be completed.
    /// </summary>
    public const string FailedMessage = "The account could not be linked. Please sign in with it again.";

    /// <summary>
    ///     The message when the browser signed in to another User than the one with the Email of the account.
    /// </summary>
    public const string WrongUserMessage = "You signed in to a different account than the one with this email address, so nothing was linked.";

    /// <summary>
    ///     The message when the account is linked to another User, or the User already has another account there linked.
    /// </summary>
    public const string LinkConflictMessage = ExternalCallbackModel.LinkConflictMessage;

    /// <summary>
    ///     The message to show.
    /// </summary>
    public string Message { get; private set; } = FailedMessage;

    /// <summary>
    ///     Where "Continue" leads.
    /// </summary>
    public string ContinueUrl { get; private set; } = SecurityModel.PagePath;

    /// <summary>
    ///     Links the pending account and continues, or shows why it was not linked.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to continue, or the page.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var pending = linkCookie.Read(Request);

        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        var result = await completeLinkService.ExecuteAsync(pending, userId, sessionId, cancellationToken);

        if (result == CompletePendingLinkResult.SignInRequired)
        {
            // Signed in before the account arrived: sign in again, then come back.
            return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");
        }

        ExternalLinkCookie.Delete(Response);

        var returnUrl = pending is not null && !string.IsNullOrEmpty(pending.ReturnUrl) && Url.IsLocalUrl(pending.ReturnUrl)
            ? pending.ReturnUrl
            : SecurityModel.PagePath;

        switch (result)
        {
            case CompletePendingLinkResult.Linked:
            case CompletePendingLinkResult.AlreadyLinked:
                return LocalRedirect(returnUrl);

            case CompletePendingLinkResult.WrongUser:
                Message = WrongUserMessage;
                ContinueUrl = returnUrl;

                return Page();

            case CompletePendingLinkResult.LinkConflict:
                Message = LinkConflictMessage;
                ContinueUrl = returnUrl;

                return Page();

            default:
                ContinueUrl = returnUrl;

                return Page();
        }
    }
}
