// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account.SignIn;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.Users.SetPassword;
using UserEntity = Authora.Core.Domain.Entities.User;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The sign-in page. After signing in, the User is sent back to the local <see cref="ReturnUrl"/>,
///     e.g. the pending authorization request.
/// </summary>
/// <param name="signInService">The Service to check the credentials and start a Session.</param>
/// <param name="signOutService">The Service to end the previous Session of the browser.</param>
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class LoginModel(SignInService signInService, SignOutService signOutService) : PageModel
{
    /// <summary>
    ///     The same message for every failure, so the page does not reveal whether an Email exists (US-007).
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
    ///     Shows the sign-in form.
    /// </summary>
    public void OnGet()
    {
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
            return Failed();
        }

        var result = await signInService.ExecuteAsync(Email, Password, cancellationToken);

        if (!result.Succeeded)
        {
            return Failed();
        }

        // A new Session token on every sign-in (US-008): the Session of the previous User of this browser ends,
        // and a token an attacker planted in the browser before the sign-in is never upgraded.
        await signOutService.ExecuteAsync(SessionCookie.Read(Request), cancellationToken);
        SessionCookie.Append(Response, result.SessionToken!);

        return LocalRedirect(GetSafeReturnUrl());
    }

    private PageResult Failed()
    {
        Password = string.Empty;
        ErrorMessage = InvalidCredentialsMessage;

        return Page();
    }

    private string GetSafeReturnUrl()
    {
        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : SessionAuthenticationHandler.SignInPath;
    }
}
