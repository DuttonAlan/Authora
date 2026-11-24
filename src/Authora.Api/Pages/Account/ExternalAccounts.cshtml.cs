// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account.SignInMethods;
using Authora.Core.Application.ExternalSignIn.GetIdentities;
using Authora.Core.Application.ExternalSignIn.Unlink;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User sees their accounts at external identity providers, links another one through
///     <see cref="ExternalLinkModel"/>, and removes a link.
/// </summary>
/// <remarks>
///     Requires a recent authentication, like the other changes to how the User signs in.
/// </remarks>
/// <param name="getExternalIdentitiesService">The Service to list the links and the providers.</param>
/// <param name="unlinkService">The Service to remove a link.</param>
/// <param name="getSignInMethodsService">The Service to tell whether the User has a password.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class ExternalAccountsModel(
    GetExternalIdentitiesService getExternalIdentitiesService,
    UnlinkExternalIdentityService unlinkService,
    GetSignInMethodsService getSignInMethodsService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/external-accounts";

    /// <summary>
    ///     The message after an account was linked.
    /// </summary>
    public const string LinkedMessage = "The account was linked. You can now sign in with it.";

    /// <summary>
    ///     The message when the account was already linked.
    /// </summary>
    public const string AlreadyLinkedMessage = "This account was already linked.";

    /// <summary>
    ///     The message after a link was removed.
    /// </summary>
    public const string UnlinkedMessage = "The account was unlinked. It can no longer be used to sign in.";

    /// <summary>
    ///     The message when the account to unlink is the only way of a User without a password to sign in.
    /// </summary>
    public const string LastSignInMethodMessage = "This account is your only way to sign in. Set a password first, then you can unlink it.";

    /// <summary>
    ///     What happened before the page was shown, e.g. <c>linked</c> after a link.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    /// <summary>
    ///     The link to remove.
    /// </summary>
    [BindProperty]
    public Guid ExternalIdentityId { get; set; }

    /// <summary>
    ///     The links of the User and the providers they can still link.
    /// </summary>
    public ExternalIdentitiesResponse Accounts { get; private set; } = new([], []);

    /// <summary>
    ///     The confirmation to show, or null.
    /// </summary>
    public string? StatusMessage => Status switch
    {
        "linked" => LinkedMessage,
        "already-linked" => AlreadyLinkedMessage,
        "unlinked" => UnlinkedMessage,
        _ => null
    };

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage => Status == "last-sign-in-method" ? LastSignInMethodMessage : null;

    /// <summary>
    ///     How the User can sign in, so the page points a User without a password to setting one.
    /// </summary>
    public SignInMethodsResponse SignInMethods { get; private set; } = new(true, 0);

    /// <summary>
    ///     Shows the links.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the page is ready.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Accounts = await getExternalIdentitiesService.ExecuteAsync(GetUserId(), cancellationToken);
        SignInMethods = await getSignInMethodsService.ExecuteAsync(GetUserId(), cancellationToken);
    }

    /// <summary>
    ///     Removes a link.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the page.</returns>
    public async Task<IActionResult> OnPostUnlinkAsync(CancellationToken cancellationToken)
    {
        var result = await unlinkService.ExecuteAsync(GetUserId(), ExternalIdentityId, cancellationToken);

        return LocalRedirect(result switch
        {
            UnlinkExternalIdentityResult.Unlinked => $"{PagePath}?status=unlinked",
            UnlinkExternalIdentityResult.LastSignInMethod => $"{PagePath}?status=last-sign-in-method",
            _ => PagePath
        });
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }
}
