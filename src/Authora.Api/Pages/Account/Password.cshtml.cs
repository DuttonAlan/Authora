// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account.AddPassword;
using Authora.Core.Application.Account.ChangePassword;
using Authora.Core.Application.Account.SignInMethods;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Exceptions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User changes their own password, or sets one if they have none (e.g. after signing
///     up through an external identity provider). Users who are not signed in are sent to the sign-in page first.
/// </summary>
/// <remarks>
///     The current password is checked like a sign-in: it shares the rate limit of the sign-in page and counts towards
///     the lockout. Setting a first password has no current password to check, so it needs a recent authentication
///     instead. After the change, only the Session of this browser stays valid.
/// </remarks>
/// <param name="changePasswordService">The Service to check the current and set the new password.</param>
/// <param name="addPasswordService">The Service to set the first password of a User without one.</param>
/// <param name="getSignInMethodsService">The Service to tell whether the User has a password.</param>
/// <param name="authorizationService">Checks the recent authentication for setting a first password.</param>
[Authorize]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class PasswordModel(
    ChangePasswordService changePasswordService,
    AddPasswordService addPasswordService,
    GetSignInMethodsService getSignInMethodsService,
    IAuthorizationService authorizationService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/password";

    /// <summary>
    ///     The message for a wrong current password.
    /// </summary>
    public const string WrongPasswordMessage = "The current password is incorrect.";

    /// <summary>
    ///     The message while the User is locked out.
    /// </summary>
    public const string LockedOutMessage = "Too many incorrect passwords. Try again later.";

    /// <summary>
    ///     The message when the User was changed at the same time, e.g. in another tab.
    /// </summary>
    public const string ConflictMessage = "Your account was changed at the same time. Please try again.";

    /// <summary>
    ///     The password the User has now. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter your current password.")]
    [StringLength(PasswordPolicy.MaxLength)]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    ///     The new password. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter a new password.")]
    [StringLength(PasswordPolicy.MaxLength, MinimumLength = PasswordPolicy.MinLength, ErrorMessage = "The new password must be {2} to {1} characters long.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    ///     The new password again, against typing errors. Never shown again on the page.
    /// </summary>
    [BindProperty]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    ///     The local URL to offer after the change, e.g. the admin interface. Other URLs are ignored.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Whether the password has just been changed.
    /// </summary>
    public bool Changed { get; private set; }

    /// <summary>
    ///     Whether the User has no password yet and sets one, without a current password.
    /// </summary>
    public bool IsSettingPassword { get; private set; }

    /// <summary>
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise null.
    /// </summary>
    public string? SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    /// <summary>
    ///     Shows the form to change the password, or to set one after a recent authentication.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page, or the sign-in page if a User without a password did not authenticate recently.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        IsSettingPassword = !(await getSignInMethodsService.ExecuteAsync(GetUserId(), cancellationToken)).HasPassword;

        return IsSettingPassword && !await IsRecentlyAuthenticatedAsync() ? Forbid() : Page();
    }

    /// <summary>
    ///     Checks the current password and sets the new one, or sets the first password of a User without one.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        IsSettingPassword = !(await getSignInMethodsService.ExecuteAsync(userId, cancellationToken)).HasPassword;

        if (IsSettingPassword)
        {
            // There is no current password to enter.
            ModelState.Remove(nameof(CurrentPassword));
        }

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;

            return Failed(firstError);
        }

        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        try
        {
            return IsSettingPassword
                ? await AddPasswordAsync(userId, sessionId, cancellationToken)
                : await ChangePasswordAsync(userId, sessionId, cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return Failed(exception.Message);
        }
        catch (ConflictException)
        {
            return Failed(ConflictMessage);
        }
    }

    private async Task<IActionResult> ChangePasswordAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await changePasswordService.ExecuteAsync(
            userId,
            sessionId,
            new ChangePasswordRequest(CurrentPassword, NewPassword),
            cancellationToken);

        switch (result)
        {
            case ChangePasswordResult.Changed:
                ClearPasswords();
                Changed = true;

                return Page();

            case ChangePasswordResult.WrongPassword:
                return Failed(WrongPasswordMessage);

            case ChangePasswordResult.LockedOut:
                return Failed(LockedOutMessage);

            case ChangePasswordResult.NoPassword:
                IsSettingPassword = true;

                return Failed(ConflictMessage);

            default:
                return SessionEnded();
        }
    }

    private async Task<IActionResult> AddPasswordAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await addPasswordService.ExecuteAsync(userId, sessionId, NewPassword, cancellationToken);

        switch (result)
        {
            case AddPasswordResult.Added:
                ClearPasswords();
                Changed = true;

                return Page();

            case AddPasswordResult.HasPassword:
                IsSettingPassword = false;

                return Failed(ConflictMessage);

            case AddPasswordResult.SignInRequired:
                ClearPasswords();

                return Forbid();

            default:
                return SessionEnded();
        }
    }

    private RedirectResult SessionEnded()
    {
        SessionCookie.Delete(Response);

        return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");
    }

    private async Task<bool> IsRecentlyAuthenticatedAsync()
    {
        return (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.RecentAuthentication)).Succeeded;
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }

    private PageResult Failed(string message)
    {
        ClearPasswords();
        ErrorMessage = message;

        return Page();
    }

    private void ClearPasswords()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well.
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ModelState.Clear();
    }
}
