// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Api.Authentication;
using Authora.Core.Application.Account.Security;
using Authora.Core.Application.Mfa.GetStatus;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The account overview of a signed-in User: how they signed in, and links to change the password, the connected
///     applications and the second factors. Users who are not signed in are sent to the sign-in page first.
/// </summary>
/// <param name="getAccountSecurityService">The Service to list the second factors of the User.</param>
/// <param name="getMfaStatusService">The Service to get the authenticator app and the recovery codes of the User.</param>
[Authorize]
public sealed class SecurityModel(GetAccountSecurityService getAccountSecurityService, GetMfaStatusService getMfaStatusService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security";

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     When the User last authenticated in the Session (UTC).
    /// </summary>
    public DateTime AuthenticatedAt { get; private set; }

    /// <summary>
    ///     How the User signed in to the Session, as names to show, e.g. "Password".
    /// </summary>
    public IReadOnlyList<string> AuthenticationMethods { get; private set; } = [];

    /// <summary>
    ///     The second factors Authora offers and whether the User enrolled in them; empty if it offers none.
    /// </summary>
    public IReadOnlyList<SecondFactorStatusResponse> SecondFactors { get; private set; } = [];

    /// <summary>
    ///     The authenticator app and the recovery codes of the User.
    /// </summary>
    public MfaStatusResponse Mfa { get; private set; } = new(false, null, null);

    /// <summary>
    ///     Shows the overview.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the overview is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // The claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var authenticationTime = long.Parse(User.FindFirst(SessionClaimTypes.AuthenticationTime)!.Value, CultureInfo.InvariantCulture);

        AuthenticatedAt = DateTimeOffset.FromUnixTimeSeconds(authenticationTime).UtcDateTime;
        SecondFactors = await getAccountSecurityService.ExecuteAsync(userId, cancellationToken);
        Mfa = await getMfaStatusService.ExecuteAsync(userId, cancellationToken);

        AuthenticationMethods = [.. User.FindAll(SessionClaimTypes.AuthenticationMethod).Select(claim => DisplayName(claim.Value))];
    }

    private string DisplayName(string method)
    {
        if (method == Core.Domain.AuthenticationMethods.Password)
        {
            return "Password";
        }

        return SecondFactors.FirstOrDefault(factor => factor.Method == method)?.DisplayName ?? method;
    }
}
