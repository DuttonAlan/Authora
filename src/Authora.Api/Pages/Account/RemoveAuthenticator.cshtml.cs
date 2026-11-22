// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa.Remove;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User removes their authenticator app and recovery codes, after entering a current
///     code or a recovery code.
/// </summary>
/// <remarks>
///     Requires a recent authentication. The code is checked like a sign-in: it shares the rate limit of the sign-in
///     page and counts towards the lockout. Afterwards only the Session of this browser stays valid.
/// </remarks>
/// <param name="removeTotpService">The Service to check the code and remove the authenticator app.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class RemoveAuthenticatorModel(RemoveTotpService removeTotpService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/authenticator/remove";

    /// <summary>
    ///     The message for a wrong code.
    /// </summary>
    public const string WrongCodeMessage = "The code is incorrect or was already used.";

    /// <summary>
    ///     The message while the User is locked out.
    /// </summary>
    public const string LockedOutMessage = "Too many incorrect attempts. Try again later.";

    /// <summary>
    ///     The message when the User was changed at the same time, e.g. in another tab.
    /// </summary>
    public const string ConflictMessage = "Your account was changed at the same time. Please try again.";

    /// <summary>
    ///     A code of the app or a recovery code. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter a code of your authenticator app or a recovery code.")]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the authenticator app has just been removed.
    /// </summary>
    public bool Removed { get; private set; }

    /// <summary>
    ///     Whether the User has no authenticator app.
    /// </summary>
    public bool NotEnrolled { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Shows the form.
    /// </summary>
    public void OnGet()
    {
    }

    /// <summary>
    ///     Checks the code and removes the authenticator app.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Failed(ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage);
        }

        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        RemoveTotpResult result;

        try
        {
            result = await removeTotpService.ExecuteAsync(userId, sessionId, Code, cancellationToken);
        }
        catch (ConflictException)
        {
            return Failed(ConflictMessage);
        }

        switch (result)
        {
            case RemoveTotpResult.Removed:
                ClearInput();
                Removed = true;

                return Page();

            case RemoveTotpResult.WrongCode:
                return Failed(WrongCodeMessage);

            case RemoveTotpResult.LockedOut:
                return Failed(LockedOutMessage);

            case RemoveTotpResult.NotEnrolled:
                ClearInput();
                NotEnrolled = true;

                return Page();

            default:
                SessionCookie.Delete(Response);

                return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");
        }
    }

    private PageResult Failed(string message)
    {
        ClearInput();
        ErrorMessage = message;

        return Page();
    }

    private void ClearInput()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well.
        Code = string.Empty;
        ModelState.Clear();
    }
}
