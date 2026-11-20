// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account.ChangePassword;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Exceptions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User changes their own password. Users who are not signed in are sent to the sign-in
///     page first.
/// </summary>
/// <remarks>
///     The current password is checked like a sign-in: it shares the rate limit of the sign-in page and counts towards
///     the lockout. After the change, only the Session of this browser stays valid.
/// </remarks>
/// <param name="changePasswordService">The Service to check the current and set the new password.</param>
[Authorize]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
public sealed class PasswordModel(ChangePasswordService changePasswordService) : PageModel
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
    ///     The Email of the signed-in User.
    /// </summary>
    public string? SignedInEmail => User.FindFirst(SessionClaimTypes.Email)?.Value;

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise null.
    /// </summary>
    public string? SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    /// <summary>
    ///     Shows the form.
    /// </summary>
    public void OnGet()
    {
    }

    /// <summary>
    ///     Checks the current password and sets the new one.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;

            return Failed(firstError);
        }

        // Both claims are always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        var userId = Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
        var sessionId = Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);

        ChangePasswordResult result;

        try
        {
            result = await changePasswordService.ExecuteAsync(
                userId,
                sessionId,
                new ChangePasswordRequest(CurrentPassword, NewPassword),
                cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return Failed(exception.Message);
        }
        catch (ConflictException)
        {
            return Failed(ConflictMessage);
        }

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

            default:
                SessionCookie.Delete(Response);

                return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");
        }
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
