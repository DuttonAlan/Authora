// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.QrCodes;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa.ConfirmEnrollment;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Mfa.StartEnrollment;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User sets up an authenticator app: they scan the QR code (or type in the secret),
///     confirm with a first code and then see their recovery codes once.
/// </summary>
/// <remarks>
///     Requires a recent authentication; otherwise the User signs in again first. The QR code is an inline SVG drawn on
///     the server, so the page needs no JavaScript and keeps its Content Security Policy. Like every page with a
///     Session, it is never cached (<see cref="SecurityHeadersPageFilter"/>), which matters here because it shows the
///     secret.
/// </remarks>
/// <param name="getMfaStatusService">The Service to check whether an authenticator app is already set up.</param>
/// <param name="startTotpEnrollmentService">The Service to create or continue the setup.</param>
/// <param name="confirmTotpEnrollmentService">The Service to confirm the setup with a first code.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class AuthenticatorModel(
    GetMfaStatusService getMfaStatusService,
    StartTotpEnrollmentService startTotpEnrollmentService,
    ConfirmTotpEnrollmentService confirmTotpEnrollmentService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/authenticator";

    /// <summary>
    ///     The message for a wrong code.
    /// </summary>
    public const string WrongCodeMessage = "The code is incorrect. Check that the time on your phone is set automatically, and enter the code the app shows now.";

    /// <summary>
    ///     The message when the setup expired before it was confirmed.
    /// </summary>
    public const string ExpiredMessage = "The setup has expired. Remove the entry from your app, then scan the new QR code.";

    /// <summary>
    ///     The message when the setup was changed at the same time, e.g. in another tab.
    /// </summary>
    public const string ConflictMessage = "Your account was changed at the same time. Please try again.";

    /// <summary>
    ///     The first code of the app. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter the code the app shows.")]
    [StringLength(32)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     The local URL to continue at once the app is set up, e.g. an authorization request that requires
    ///     multi-factor authentication. Other URLs are ignored.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise null.
    /// </summary>
    public string? SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    /// <summary>
    ///     Where to continue once the app is set up: the <see cref="SafeReturnUrl"/>, otherwise the account overview.
    /// </summary>
    public string ContinueUrl => SafeReturnUrl ?? SecurityModel.PagePath;

    /// <summary>
    ///     The secret and URI to add to the app, or null if there is nothing to set up.
    /// </summary>
    public TotpEnrollmentResponse? Enrollment { get; private set; }

    /// <summary>
    ///     The path data of the QR code, see <see cref="QrCode.ToSvgPath"/>.
    /// </summary>
    public string QrCodePath { get; private set; } = string.Empty;

    /// <summary>
    ///     Width and height of the QR code with its border, for the <c>viewBox</c>.
    /// </summary>
    public int QrCodeSize { get; private set; }

    /// <summary>
    ///     The recovery codes, once the setup is confirmed; empty before.
    /// </summary>
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];

    /// <summary>
    ///     Whether the User already has an authenticator app.
    /// </summary>
    public bool AlreadyEnrolled { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Shows the QR code and the form for the first code.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    /// <summary>
    ///     Checks the first code and finishes the setup.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the recovery codes, or with an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FailedAsync(ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage, cancellationToken);
        }

        ConfirmTotpEnrollmentResult result;

        try
        {
            result = await confirmTotpEnrollmentService.ExecuteAsync(GetUserId(), Code, cancellationToken);
        }
        catch (ConflictException)
        {
            return await FailedAsync(ConflictMessage, cancellationToken);
        }

        switch (result.Status)
        {
            case ConfirmTotpEnrollmentStatus.Confirmed:
                ClearInput();
                RecoveryCodes = result.RecoveryCodes;

                return Page();

            case ConfirmTotpEnrollmentStatus.WrongCode:
                return await FailedAsync(WrongCodeMessage, cancellationToken);

            case ConfirmTotpEnrollmentStatus.Expired:
                return await FailedAsync(ExpiredMessage, cancellationToken);

            default:
                ClearInput();
                AlreadyEnrolled = true;

                return Page();
        }
    }

    private async Task<PageResult> FailedAsync(string message, CancellationToken cancellationToken)
    {
        ClearInput();
        await LoadAsync(cancellationToken);
        ErrorMessage = message;

        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if ((await getMfaStatusService.ExecuteAsync(userId, cancellationToken)).Enabled)
        {
            AlreadyEnrolled = true;

            return;
        }

        try
        {
            Enrollment = await startTotpEnrollmentService.ExecuteAsync(userId, cancellationToken);
        }
        catch (ConflictException)
        {
            ErrorMessage = ConflictMessage;

            return;
        }

        var qrCode = QrCode.Encode(Enrollment.Uri);

        QrCodePath = qrCode.ToSvgPath();
        QrCodeSize = qrCode.Size + (2 * QrCode.QuietZone);
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }

    private void ClearInput()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well.
        Code = string.Empty;
        ModelState.Clear();
    }
}
