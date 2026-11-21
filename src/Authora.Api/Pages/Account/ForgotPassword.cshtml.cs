// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.RateLimiting;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.RequestPasswordReset;
using Authora.Core.Application.Email;
using UserEntity = Authora.Core.Domain.Entities.User;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a User who forgot their password asks for a reset link by email ("Forgot password?").
/// </summary>
/// <remarks>
///     Answers every address the same way, so it does not reveal whether an account exists. Not found while the reset
///     is not available, see <see cref="RequestPasswordResetService.IsAvailable"/>.
/// </remarks>
/// <param name="requestPasswordResetService">The Service to send the link.</param>
/// <param name="options">The password reset settings with the lifetime of the link.</param>
[EnableRateLimiting(RateLimitPolicies.PasswordReset)]
public sealed class ForgotPasswordModel(
    RequestPasswordResetService requestPasswordResetService,
    IOptions<PasswordResetOptions> options) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/forgot-password";

    /// <summary>
    ///     The confirmation after a request, the same whether or not an account has the address.
    /// </summary>
    public const string SentMessage = "If an account with this email address exists, we sent it a link to choose a new password.";

    /// <summary>
    ///     The address entered by the User.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Enter your email address.")]
    [StringLength(UserEntity.EmailMaxLength)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the request was just sent.
    /// </summary>
    public bool Sent { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     How long a link works, for the page, e.g. <c>30 minutes</c>.
    /// </summary>
    public string LinkLifetime => EmailTemplates.FormatDuration(options.Value.LinkLifetime);

    /// <summary>
    ///     Shows the form.
    /// </summary>
    /// <returns>The page, or 404 Not Found if the reset is not available.</returns>
    public IActionResult OnGet()
    {
        return requestPasswordResetService.IsAvailable ? Page() : NotFound();
    }

    /// <summary>
    ///     Sends a reset link if an account has the address, and shows the same confirmation either way.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with the confirmation or an error, or 404 Not Found if the reset is not available.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!requestPasswordResetService.IsAvailable)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;

            return Page();
        }

        await requestPasswordResetService.ExecuteAsync(new RequestPasswordResetRequest(Email), cancellationToken);

        Sent = true;

        return Page();
    }
}
