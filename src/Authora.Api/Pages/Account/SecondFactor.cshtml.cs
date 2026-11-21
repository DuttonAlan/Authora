// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account.CompleteSignIn;
using Authora.Core.Application.Account.PendingSignIn;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Domain;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The second step of signing in: the User completes a second factor for the pending sign-in in the cookie, after
///     the password on the sign-in page or for a step-up of their Session.
/// </summary>
/// <remarks>
///     It shares the rate limit of the sign-in page, and wrong responses count towards the lockout. Only once the
///     factor is complete does the browser get a Session cookie (or, for a step-up, a new one).
/// </remarks>
/// <param name="getPendingSignInService">The Service to load the pending sign-in.</param>
/// <param name="completeSignInService">The Service to check the factor and complete the sign-in.</param>
/// <param name="signOutService">The Service to end the previous Session of the browser.</param>
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class SecondFactorModel(
    GetPendingSignInService getPendingSignInService,
    CompleteSignInService completeSignInService,
    SignOutService signOutService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/second-factor";

    /// <summary>
    ///     The message when the pending sign-in can no longer be completed.
    /// </summary>
    public const string ExpiredMessage = "Your sign-in has expired or was changed in the meantime. Please sign in again.";

    /// <summary>
    ///     The message for a wrong response.
    /// </summary>
    public const string WrongResponseMessage = "The code is incorrect.";

    /// <summary>
    ///     The message while the User is locked out.
    /// </summary>
    public const string LockedOutMessage = "Too many incorrect attempts. Try again later.";

    /// <summary>
    ///     Longest response accepted, e.g. a code.
    /// </summary>
    public const int MaxResponseLength = 4096;

    /// <summary>
    ///     The method of the factor the User completes, e.g. <c>otp</c>.
    /// </summary>
    [BindProperty]
    [StringLength(AuthenticationMethods.MaxMethodLength)]
    public string? Method { get; set; }

    /// <summary>
    ///     The response the User entered, e.g. a code. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter the code.")]
    [StringLength(MaxResponseLength)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     The local URL to return to afterwards. Other URLs are ignored, so the page is not an open redirect.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The pending sign-in, or null if it can no longer be completed.
    /// </summary>
    public PendingSignInResponse? Pending { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise null.
    /// </summary>
    public string? SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    /// <summary>
    ///     The sign-in page, with the way back to the <see cref="ReturnUrl"/>.
    /// </summary>
    public string SignInUrl => SafeReturnUrl is null
        ? SessionAuthenticationHandler.SignInPath
        : $"{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl)}";

    /// <summary>
    ///     Where "Cancel" leads: back for a step-up, which keeps the Session as it is, otherwise to the sign-in page.
    /// </summary>
    public string CancelUrl => Pending is { IsStepUp: true } ? SafeReturnUrl ?? SecurityModel.PagePath : SignInUrl;

    /// <summary>
    ///     Shows the form for the pending sign-in of the browser.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the form, or with a link to sign in again.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPendingAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    ///     Checks the response and completes the sign-in.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the <see cref="ReturnUrl"/>, or the page with an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;

            return await FailedAsync(firstError, cancellationToken);
        }

        var result = await completeSignInService.ExecuteAsync(
            new CompleteSignInRequest(PendingSignInCookie.Read(Request), Method ?? string.Empty, Code),
            GetCurrentSessionId(),
            cancellationToken);

        switch (result.Status)
        {
            case CompleteSignInStatus.SignedIn:
                // Like on the sign-in page: the Session of the previous User of this browser ends.
                await signOutService.ExecuteAsync(SessionCookie.Read(Request), cancellationToken);
                SessionCookie.Append(Response, result.SessionToken!);
                PendingSignInCookie.Delete(Response);

                return LocalRedirect(SafeReturnUrl ?? SessionAuthenticationHandler.SignInPath);

            case CompleteSignInStatus.SteppedUp:
                // The same Session with a new token; the old token no longer works.
                SessionCookie.Append(Response, result.SessionToken!);
                PendingSignInCookie.Delete(Response);

                return LocalRedirect(SafeReturnUrl ?? SessionAuthenticationHandler.SignInPath);

            case CompleteSignInStatus.WrongResponse:
                return await FailedAsync(WrongResponseMessage, cancellationToken);

            case CompleteSignInStatus.LockedOut:
                PendingSignInCookie.Delete(Response);
                ClearInput();
                ErrorMessage = LockedOutMessage;

                return Page();

            default:
                PendingSignInCookie.Delete(Response);
                ClearInput();

                return Page();
        }
    }

    private async Task<PageResult> FailedAsync(string message, CancellationToken cancellationToken)
    {
        ClearInput();
        await LoadPendingAsync(cancellationToken);

        if (Pending is not null)
        {
            ErrorMessage = message;
        }

        return Page();
    }

    private async Task LoadPendingAsync(CancellationToken cancellationToken)
    {
        Pending = await getPendingSignInService.ExecuteAsync(PendingSignInCookie.Read(Request), GetCurrentSessionId(), cancellationToken);

        if (Pending is null)
        {
            PendingSignInCookie.Delete(Response);

            return;
        }

        if (Method is null || !Pending.Factors.Any(factor => factor.Method == Method))
        {
            Method = Pending.Factors[0].Method;
        }
    }

    private Guid? GetCurrentSessionId()
    {
        return Guid.TryParse(User.FindFirst(SessionClaimTypes.SessionId)?.Value, out var sessionId) ? sessionId : null;
    }

    private void ClearInput()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well. The chosen
        // factor is kept.
        Code = string.Empty;
        ModelState.Clear();
    }
}
