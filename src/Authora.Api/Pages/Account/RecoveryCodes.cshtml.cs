// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Mfa.RegenerateRecoveryCodes;

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
///     Requires a recent authentication, since new codes sign in as well as the authenticator app.
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
    /// <returns>The page with the new codes, or with an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        try
        {
            NewCodes = await regenerateRecoveryCodesService.ExecuteAsync(userId, cancellationToken);
        }
        catch (ConflictException)
        {
            ErrorMessage = ConflictMessage;
        }

        Status = await getMfaStatusService.ExecuteAsync(userId, cancellationToken);

        return Page();
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }
}
