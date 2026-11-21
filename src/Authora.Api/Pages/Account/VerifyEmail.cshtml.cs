// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.VerifyEmail;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page an email verification link opens, where the User confirms their address.
/// </summary>
/// <remarks>
///     Opening the link only shows a button; the address is confirmed when it is submitted, so a mail scanner that
///     follows links does not use the link up. No sign-in is needed: the link proves that whoever opens it receives
///     email at the address.
/// </remarks>
/// <param name="verifyEmailService">The Service to check the link and confirm the address.</param>
[EnableRateLimiting(RateLimitPolicies.PasswordReset)]
public sealed class VerifyEmailModel(VerifyEmailService verifyEmailService) : PageModel
{
    /// <summary>
    ///     The message for a link that does not work.
    /// </summary>
    public const string InvalidLinkMessage = "This link does not work anymore. It may have expired, been used already, or been replaced by a newer link, or the email address of the account changed.";

    /// <summary>
    ///     The message when the User was changed at the same time.
    /// </summary>
    public const string ConflictMessage = "Your account was changed at the same time, so the email address was not confirmed.";

    /// <summary>
    ///     The message once the address is confirmed.
    /// </summary>
    public const string VerifiedMessage = "Your email address is confirmed.";

    /// <summary>
    ///     The one-time token from the link. Kept in the form, so it is sent again on confirmation.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = EmailVerificationLink.TokenParameter)]
    [StringLength(EmailTokenRedeemer.MaxTokenLength)]
    public string? Token { get; set; }

    /// <summary>
    ///     Whether the link works, so the button is shown.
    /// </summary>
    public bool IsValidLink { get; private set; }

    /// <summary>
    ///     Whether the address has just been confirmed.
    /// </summary>
    public bool Verified { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Shows the button if the link works.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the button, or with an error if the link does not work.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        IsValidLink = ModelState.IsValid && await verifyEmailService.IsValidLinkAsync(Token, cancellationToken);

        if (!IsValidLink)
        {
            Token = null;
            ErrorMessage = InvalidLinkMessage;
        }

        return Page();
    }

    /// <summary>
    ///     Uses up the link and confirms the address.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        VerifyEmailResult result;

        try
        {
            result = ModelState.IsValid
                ? await verifyEmailService.ExecuteAsync(Token, cancellationToken)
                : VerifyEmailResult.InvalidLink;
        }
        catch (ConflictException)
        {
            return Failed(ConflictMessage);
        }

        if (result != VerifyEmailResult.Verified)
        {
            return Failed(InvalidLinkMessage);
        }

        Token = null;
        Verified = true;

        return Page();
    }

    private PageResult Failed(string message)
    {
        // The link is used up or never worked, so the form is not shown again.
        Token = null;
        ErrorMessage = message;
        ModelState.Clear();

        return Page();
    }
}
