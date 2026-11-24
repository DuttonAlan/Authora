// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.RequestEmailVerification;
using Authora.Core.Application.Account.RequestPasswordReset;
using Authora.Core.Application.Account.SignIn;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.ExternalSignIn;
using Authora.Core.Application.ExternalSignIn.GetProviders;
using Authora.Core.Application.Users.SetPassword;
using UserEntity = Authora.Core.Domain.Entities.User;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The sign-in page. After signing in, the User is sent back to the local <see cref="ReturnUrl"/>,
///     e.g. the pending authorization request.
/// </summary>
/// <param name="signInService">The Service to check the credentials and start a Session.</param>
/// <param name="signOutService">The Service to end the previous Session of the browser.</param>
/// <param name="requestPasswordResetService">Tells whether the page offers "Forgot password?".</param>
/// <param name="requestEmailVerificationService">Tells whether the page offers a signed-in User a new email verification link.</param>
/// <param name="getSignInProvidersService">The Service to list the external identity providers, one button each.</param>
/// <param name="signInOptions">The sign-in settings with the lifetime of a pending sign-in.</param>
/// <remarks>
///     A User who enrolled a second factor is sent on to <see cref="SecondFactorModel"/> after the password, with a
///     pending sign-in in its own cookie instead of a Session.
/// </remarks>
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class LoginModel(
    SignInService signInService,
    SignOutService signOutService,
    RequestPasswordResetService requestPasswordResetService,
    RequestEmailVerificationService requestEmailVerificationService,
    GetSignInProvidersService getSignInProvidersService,
    IOptions<SignInOptions> signInOptions) : PageModel
{
    /// <summary>
    ///     Whether the page links to the "Forgot password?" page: email delivery is configured and the self-service reset
    ///     is enabled.
    /// </summary>
    public bool CanResetPassword => requestPasswordResetService.IsAvailable;

    /// <summary>
    ///     The same message for every failure, so the page does not reveal whether an Email exists.
    /// </summary>
    public const string InvalidCredentialsMessage = "The email or password is incorrect, or the account cannot sign in right now.";

    /// <summary>
    ///     The Email entered by the User.
    /// </summary>
    [BindProperty]
    [Required]
    [StringLength(UserEntity.EmailMaxLength)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     The password entered by the User. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required]
    [StringLength(PasswordPolicy.MaxLength)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     The local URL to return to after signing in. Other URLs are ignored, so the page is not an open redirect.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Whether the User is already signed in.
    /// </summary>
    public bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    /// <summary>
    ///     The Email of the signed-in User, or null.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     Whether the signed-in User's Email is not verified and a new link can be requested, so the page, which the User
    ///     sees after signing in without a <see cref="ReturnUrl"/>, offers one.
    /// </summary>
    public bool CanRequestEmailVerification { get; private set; }

    /// <summary>
    ///     The enabled external identity providers, one button each, in their order.
    /// </summary>
    public IReadOnlyList<SignInProviderResponse> Providers { get; private set; } = [];

    /// <summary>
    ///     The URL of the button of a provider, with the way back to the <see cref="ReturnUrl"/>.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The URL that starts the sign-in through the provider.</returns>
    public string GetProviderUrl(SignInProviderResponse provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return $"{ExternalSignInOptions.GetStartPath(provider.Slug)}?returnUrl={Uri.EscapeDataString(GetSafeReturnUrl())}";
    }

    /// <summary>
    ///     Shows the sign-in form, or that the User is signed in.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the page is ready.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Providers = await getSignInProvidersService.ExecuteAsync(cancellationToken);

        // Only the page for a signed-in User without a ReturnUrl shows the notice, see Login.cshtml.
        if (IsSignedIn && ReturnUrl is null)
        {
            var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);

            CanRequestEmailVerification = await requestEmailVerificationService.CanRequestAsync(userId, cancellationToken);
        }
    }

    /// <summary>
    ///     Checks the credentials, starts a new Session and returns to the <see cref="ReturnUrl"/>.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A redirect to the <see cref="ReturnUrl"/>, or the page with an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FailedAsync(cancellationToken);
        }

        var result = await signInService.ExecuteAsync(Email, Password, cancellationToken);

        if (result.IsPending)
        {
            // No Session yet: the browser keeps its current Session, if any, until the second factor is complete.
            PendingSignInCookie.Append(Response, result.ChallengeToken!, signInOptions.Value.PendingSignInLifetime);

            return LocalRedirect($"{SecondFactorModel.PagePath}?returnUrl={Uri.EscapeDataString(GetSafeReturnUrl())}");
        }

        if (!result.Succeeded)
        {
            return await FailedAsync(cancellationToken);
        }

        // A new Session token on every sign-in: the Session of the previous User of this browser ends,
        // and a token an attacker planted in the browser before the sign-in is never upgraded.
        await signOutService.ExecuteAsync(SessionCookie.Read(Request), cancellationToken);
        SessionCookie.Append(Response, result.SessionToken!);
        PendingSignInCookie.Delete(Response);

        if (result.MustEnrollSecondFactor)
        {
            // Multi-factor authentication is required for administrators: set up the factor first, then continue.
            return LocalRedirect($"{AuthenticatorModel.PagePath}?returnUrl={Uri.EscapeDataString(GetSafeReturnUrl())}");
        }

        return LocalRedirect(GetSafeReturnUrl());
    }

    private async Task<PageResult> FailedAsync(CancellationToken cancellationToken)
    {
        Password = string.Empty;
        ErrorMessage = InvalidCredentialsMessage;
        Providers = await getSignInProvidersService.ExecuteAsync(cancellationToken);

        return Page();
    }

    private string GetSafeReturnUrl()
    {
        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : SessionAuthenticationHandler.SignInPath;
    }
}
