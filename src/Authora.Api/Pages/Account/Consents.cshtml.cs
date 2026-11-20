// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Account.RevokeConsent;
using Authora.Core.Application.Consents;
using Authora.Core.Application.Consents.GetAll;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User sees the applications they approved and withdraws their consent. Users who
///     are not signed in are sent to the sign-in page first.
/// </summary>
/// <remarks>
///     Withdrawing the consent also revokes the application's Access and Refresh Tokens for the User, so it is signed
///     out of the application at its next refresh, and the User is asked again on its next sign-in.
/// </remarks>
/// <param name="getUserConsentsService">The Service to list the Consents of the User.</param>
/// <param name="revokeOwnConsentService">The Service to revoke a Consent of the User.</param>
[Authorize]
public sealed class ConsentsModel(
    GetUserConsentsService getUserConsentsService,
    RevokeOwnConsentService revokeOwnConsentService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/consents";

    /// <summary>
    ///     The Consents of the User, ordered by the Name of the Client.
    /// </summary>
    public IReadOnlyList<ConsentResponse> Consents { get; private set; } = [];

    /// <summary>
    ///     The Name of the Client whose consent was just withdrawn, or null.
    /// </summary>
    public string? RevokedClientName { get; private set; }

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     Shows the Consents of the User.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Consents are loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Consents = await getUserConsentsService.ExecuteAsync(GetUserId(), cancellationToken);
    }

    /// <summary>
    ///     Withdraws the consent for a Client and shows the remaining Consents.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation.</returns>
    public async Task<IActionResult> OnPostAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var before = await getUserConsentsService.ExecuteAsync(userId, cancellationToken);

        if (await revokeOwnConsentService.ExecuteAsync(userId, clientId, cancellationToken))
        {
            RevokedClientName = before.FirstOrDefault(consent => consent.ClientId == clientId)?.ClientName;
        }

        Consents = await getUserConsentsService.ExecuteAsync(userId, cancellationToken);

        return Page();
    }

    /// <summary>
    ///     The Id of the signed-in User. Always set by SessionAuthenticationHandler, the only scheme that signs Users
    ///     in here.
    /// </summary>
    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }
}
