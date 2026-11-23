// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.RequestEmailVerification;
using Authora.Core.Application.Email;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User whose Email is not verified sends themselves a new verification link. The account
///     overview and the page after sign-in submit to it directly. Users who are not signed in are sent to the sign-in
///     page first.
/// </summary>
/// <remarks>
///     Not found while email delivery is not configured, see <see cref="RequestEmailVerificationService.IsAvailable"/>.
/// </remarks>
/// <param name="requestEmailVerificationService">The Service to send the link.</param>
/// <param name="options">The email verification settings with the lifetime of the link.</param>
[Authorize]
[EnableRateLimiting(RateLimitPolicies.EmailVerification)]
public sealed class RequestEmailVerificationModel(
    RequestEmailVerificationService requestEmailVerificationService,
    IOptions<EmailVerificationOptions> options) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/email-verification";

    /// <summary>
    ///     The confirmation once a link was sent.
    /// </summary>
    public const string SentMessage = "We sent a new link to confirm your email address. Earlier links no longer work.";

    /// <summary>
    ///     The message when the address is already confirmed.
    /// </summary>
    public const string AlreadyVerifiedMessage = "Your email address is already confirmed.";

    /// <summary>
    ///     The error when the User got too many links in the last hour.
    /// </summary>
    public const string TooManyLinksMessage = "We already sent you several links in the last hour. Use the newest one, or try again later.";

    /// <summary>
    ///     The error when the mail server did not accept the email.
    /// </summary>
    public const string DeliveryFailedMessage = "The email could not be sent. Try again later, or ask your administrator.";

    /// <summary>
    ///     The error when the account can no longer sign in.
    /// </summary>
    public const string CannotSignInMessage = "Your account cannot sign in right now, so no link was sent.";

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     Whether the form to send a link is shown.
    /// </summary>
    public bool CanRequest { get; private set; }

    /// <summary>
    ///     Whether a link was just sent.
    /// </summary>
    public bool Sent { get; private set; }

    /// <summary>
    ///     The status to show instead of the form, or null.
    /// </summary>
    public string? StatusMessage { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     How long a link works, for the page, e.g. <c>24 hours</c>.
    /// </summary>
    public string LinkLifetime => EmailTemplates.FormatDuration(options.Value.LinkLifetime);

    /// <summary>
    ///     Shows the form, or that the address is already confirmed.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page, or 404 Not Found if email delivery is not configured.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!requestEmailVerificationService.IsAvailable)
        {
            return NotFound();
        }

        CanRequest = await requestEmailVerificationService.CanRequestAsync(GetUserId(), cancellationToken);

        if (!CanRequest)
        {
            StatusMessage = AlreadyVerifiedMessage;
        }

        return Page();
    }

    /// <summary>
    ///     Sends a new link to the address of the User.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, or 404 Not Found if email delivery is not configured.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!requestEmailVerificationService.IsAvailable)
        {
            return NotFound();
        }

        var result = await requestEmailVerificationService.ExecuteAsync(GetUserId(), cancellationToken);

        switch (result)
        {
            case RequestEmailVerificationResult.Sent:
                Sent = true;
                break;
            case RequestEmailVerificationResult.AlreadyVerified:
                StatusMessage = AlreadyVerifiedMessage;
                break;
            case RequestEmailVerificationResult.TooManyLinks:
                ErrorMessage = TooManyLinksMessage;
                break;
            case RequestEmailVerificationResult.DeliveryFailed:
                ErrorMessage = DeliveryFailedMessage;
                CanRequest = true;
                break;
            default:
                ErrorMessage = CannotSignInMessage;
                break;
        }

        return Page();
    }

    // The claim is always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
    private Guid GetUserId() => Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
}
