// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.RequestPasswordReset;
using Authora.Core.Application.Account.ResetPassword;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Exceptions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page a password reset link opens, where the User chooses a new password.
/// </summary>
/// <remarks>
///     Opening the link does not use it up, so a mail scanner that follows links does not break it; only setting the
///     password does. Works for links sent by an administrator even while the self-service reset is disabled.
/// </remarks>
/// <param name="resetPasswordService">The Service to check the link and set the new password.</param>
/// <param name="requestPasswordResetService">Tells whether a new link can be requested.</param>
[EnableRateLimiting(RateLimitPolicies.PasswordReset)]
public sealed class ResetPasswordModel(
    ResetPasswordService resetPasswordService,
    RequestPasswordResetService requestPasswordResetService) : PageModel
{
    /// <summary>
    ///     The message for a link that does not work.
    /// </summary>
    public const string InvalidLinkMessage = "This link does not work anymore. It may have expired, been used already, or been replaced by a newer link.";

    /// <summary>
    ///     The message when the User was changed at the same time.
    /// </summary>
    public const string ConflictMessage = "Your account was changed at the same time, so the password was not set. Please request a new link.";

    /// <summary>
    ///     The one-time token from the link. Kept in the form, so it is sent again with the new password.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = PasswordResetLink.TokenParameter)]
    [StringLength(EmailTokenRedeemer.MaxTokenLength)]
    public string? Token { get; set; }

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
    ///     Whether the link works, so the form is shown.
    /// </summary>
    public bool IsValidLink { get; private set; }

    /// <summary>
    ///     Whether the password has just been set.
    /// </summary>
    public bool Reset { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Whether the User can request a new link on the "Forgot password?" page.
    /// </summary>
    public bool CanRequestNewLink => requestPasswordResetService.IsAvailable;

    /// <summary>
    ///     Shows the form if the link works.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the form, or with an error if the link does not work.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        IsValidLink = await resetPasswordService.IsValidLinkAsync(Token, cancellationToken);

        if (!IsValidLink)
        {
            ErrorMessage = InvalidLinkMessage;
        }

        return Page();
    }

    /// <summary>
    ///     Uses up the link and sets the new password.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;

            // The link is not used up by invalid input, so the form stays, if the link still works.
            IsValidLink = await resetPasswordService.IsValidLinkAsync(Token, cancellationToken);

            return Failed(IsValidLink ? firstError : InvalidLinkMessage);
        }

        ResetPasswordResult result;

        try
        {
            result = await resetPasswordService.ExecuteAsync(new ResetPasswordRequest(Token ?? string.Empty, NewPassword), cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            IsValidLink = true;

            return Failed(exception.Message);
        }
        catch (ConflictException)
        {
            return Failed(ConflictMessage);
        }

        if (result != ResetPasswordResult.Reset)
        {
            return Failed(InvalidLinkMessage);
        }

        // A Session of this browser has ended with the new password; the stale cookie is removed.
        SessionCookie.Delete(Response);
        ClearInput();
        Reset = true;

        return Page();
    }

    private PageResult Failed(string message)
    {
        ClearInput();
        ErrorMessage = message;

        return Page();
    }

    private void ClearInput()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well. The token stays
        // in the form only while the link still works.
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;

        if (!IsValidLink)
        {
            Token = null;
        }

        ModelState.Clear();
    }
}
