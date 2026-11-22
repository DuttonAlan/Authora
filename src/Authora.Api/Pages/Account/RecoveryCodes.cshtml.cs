// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Mfa.RegenerateRecoveryCodes;
using Authora.Core.Domain;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User sees how many recovery codes they have left and generates a new set, which
///     is shown once and replaces the previous codes.
/// </summary>
/// <remarks>
///     Requires a recent authentication, and a Session in which the User completed the authenticator app: new codes
///     sign in as well as the app. A Session signed in with the password alone is sent to the step-up first.
/// </remarks>
/// <param name="getMfaStatusService">The Service to count the remaining codes.</param>
/// <param name="regenerateRecoveryCodesService">The Service to generate a new set.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class RecoveryCodesModel(
    GetMfaStatusService getMfaStatusService,
    RegenerateRecoveryCodesService regenerateRecoveryCodesService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/recovery-codes";

    /// <summary>
    ///     The message when the codes were changed at the same time, e.g. in another tab.
    /// </summary>
    public const string ConflictMessage = "Your recovery codes were changed at the same time. Please try again.";

    /// <summary>
    ///     The multi-factor authentication of the User.
    /// </summary>
    public MfaStatusResponse? Status { get; private set; }

    /// <summary>
    ///     The new codes, right after generating them; empty otherwise.
    /// </summary>
    public IReadOnlyList<string> NewCodes { get; private set; } = [];

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Whether the Session lacks the authenticator app, so the User must confirm with it before generating codes.
    /// </summary>
    public bool NeedsStepUp => !User.FindAll(SessionClaimTypes.AuthenticationMethod)
        .Any(claim => claim.Value == AuthenticationMethods.OneTimeCode);

    /// <summary>
    ///     The step-up that adds the authenticator app to the Session and returns here.
    /// </summary>
    public static string StepUpUrl => $"{StepUpModel.PagePath}?returnUrl={Uri.EscapeDataString(PagePath)}";

    /// <summary>
    ///     Shows how many codes are left.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Status = await getMfaStatusService.ExecuteAsync(GetUserId(), cancellationToken);
    }

    /// <summary>
    ///     Generates a new set of codes.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the new codes or an error, the step-up, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = GetUserId();
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        RegenerateRecoveryCodesResult result;

        try
        {
            result = await regenerateRecoveryCodesService.ExecuteAsync(userId, sessionId, cancellationToken);
        }
        catch (ConflictException)
        {
            result = RegenerateRecoveryCodesResult.NotEnrolled;
            ErrorMessage = ConflictMessage;
        }

        switch (result.Status)
        {
            case RegenerateRecoveryCodesStatus.StepUpRequired:
                return LocalRedirect(StepUpUrl);

            case RegenerateRecoveryCodesStatus.SessionEnded:
                SessionCookie.Delete(Response);

                return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");

            default:
                NewCodes = result.RecoveryCodes;
                Status = await getMfaStatusService.ExecuteAsync(userId, cancellationToken);

                return Page();
        }
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }
}
