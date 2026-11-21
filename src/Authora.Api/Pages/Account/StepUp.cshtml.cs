// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.StepUp;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.Account;

/// <summary>
///     Starts a step-up: the signed-in User completes an additional factor for the current Session on
///     <see cref="SecondFactorModel"/>, and is then sent back to the <see cref="ReturnUrl"/>. Users who are not signed in
///     are sent to the sign-in page first.
/// </summary>
/// <remarks>
///     Starting only creates a pending sign-in; the Session stays as it is until the factor is complete.
/// </remarks>
/// <param name="startStepUpService">The Service to start the step-up.</param>
/// <param name="signInOptions">The sign-in settings with the lifetime of a pending sign-in.</param>
[Authorize]
public sealed class StepUpModel(StartStepUpService startStepUpService, IOptions<SignInOptions> signInOptions) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/step-up";

    /// <summary>
    ///     The local URL to return to afterwards. Other URLs are ignored.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise the account overview.
    /// </summary>
    public string SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : SecurityModel.PagePath;

    /// <summary>
    ///     Starts the step-up and sends the User to the second factor, or shows that there is nothing to complete.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the second factor, or the page.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        var challengeToken = await startStepUpService.ExecuteAsync(userId, sessionId, cancellationToken);

        if (challengeToken is null)
        {
            return Page();
        }

        PendingSignInCookie.Append(Response, challengeToken, signInOptions.Value.PendingSignInLifetime);

        return LocalRedirect($"{SecondFactorModel.PagePath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl)}");
    }
}
