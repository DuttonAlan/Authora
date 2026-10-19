// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Account.SignOut;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The sign-out page. Signing out needs a POST with an antiforgery token, so another site cannot sign the User out.
/// </summary>
/// <param name="signOutService">The Service to end the Session.</param>
public sealed class LogoutModel(SignOutService signOutService) : PageModel
{
    /// <summary>
    ///     Whether the User has just signed out.
    /// </summary>
    public bool SignedOut { get; private set; }

    /// <summary>
    ///     Shows the sign-out confirmation.
    /// </summary>
    public void OnGet()
    {
    }

    /// <summary>
    ///     Ends the Session on the server and deletes the cookie.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await signOutService.ExecuteAsync(SessionCookie.Read(Request), cancellationToken);
        SessionCookie.Delete(Response);

        SignedOut = true;

        return Page();
    }
}
