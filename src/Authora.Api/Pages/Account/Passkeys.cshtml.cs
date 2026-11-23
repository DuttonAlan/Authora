// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.RateLimiting;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Passkeys;
using Authora.Core.Application.Passkeys.Register;
using Authora.Core.Application.Passkeys.Remove;
using Authora.Core.Application.Passkeys.Rename;
using Authora.Core.Application.Passkeys.StartRegistration;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.Pages.Account;

/// <summary>
///     The page where a signed-in User registers, renames and removes their passkeys.
/// </summary>
/// <remarks>
///     <para>
///         Requires a recent authentication, and, if the User has a second factor, a Session in which they completed
///         one (otherwise they step up first, see <see cref="PasskeyChangeGuard"/>). It shares the rate limit of the
///         sign-in page.
///     </para>
///     <para>
///         A passkey is registered with the script <c>js/passkeys.js</c>: it gets the options from
///         <see cref="OnPostRegistrationOptionsAsync"/>, lets the browser create the passkey and posts the response as
///         <see cref="Credential"/> to <see cref="OnPostRegisterAsync"/>. The page therefore allows scripts of its own
///         origin (<see cref="AllowScriptsAttribute"/>).
///     </para>
/// </remarks>
/// <param name="guard">Checks whether the Session must step up first.</param>
/// <param name="getMfaStatusService">The Service to list the passkeys.</param>
/// <param name="startPasskeyRegistrationService">The Service to create the options for a new passkey.</param>
/// <param name="registerPasskeyService">The Service to verify and store a new passkey.</param>
/// <param name="renamePasskeyService">The Service to rename a passkey.</param>
/// <param name="removePasskeyService">The Service to remove a passkey.</param>
[Authorize(Policy = AuthorizationPolicies.RecentAuthentication)]
[EnableRateLimiting(RateLimitPolicies.SignIn)]
[AllowScripts]
public sealed class PasskeysModel(
    PasskeyChangeGuard guard,
    GetMfaStatusService getMfaStatusService,
    StartPasskeyRegistrationService startPasskeyRegistrationService,
    RegisterPasskeyService registerPasskeyService,
    RenamePasskeyService renamePasskeyService,
    RemovePasskeyService removePasskeyService) : PageModel
{
    /// <summary>
    ///     Path of the page.
    /// </summary>
    public const string PagePath = "/account/security/passkeys";

    /// <summary>
    ///     The message for a registration that was not accepted.
    /// </summary>
    public const string RejectedMessage = "The passkey could not be registered. Please try again.";

    /// <summary>
    ///     The message for a passkey that is already registered.
    /// </summary>
    public const string AlreadyRegisteredMessage = "This passkey is already registered.";

    /// <summary>
    ///     The message when the User has the maximum number of passkeys.
    /// </summary>
    public const string LimitReachedMessage = "You have the maximum number of passkeys. Remove one before you add another.";

    /// <summary>
    ///     The message when the passkeys were changed at the same time, e.g. in another tab.
    /// </summary>
    public const string ConflictMessage = "Your passkeys were changed at the same time. Please try again.";

    /// <summary>
    ///     The name of a new passkey.
    /// </summary>
    [BindProperty]
    [StringLength(Passkey.NameMaxLength, ErrorMessage = "The name must be at most 64 characters long.")]
    public string? Name { get; set; }

    /// <summary>
    ///     The response of <c>navigator.credentials.create</c> as JSON, set by the script.
    /// </summary>
    [BindProperty]
    [StringLength(SecondFactorModel.MaxResponseLength)]
    public string? Credential { get; set; }

    /// <summary>
    ///     The passkey to rename or remove.
    /// </summary>
    [BindProperty]
    public Guid PasskeyId { get; set; }

    /// <summary>
    ///     The new name of the passkey to rename.
    /// </summary>
    [BindProperty]
    [StringLength(Passkey.NameMaxLength, ErrorMessage = "The name must be at most 64 characters long.")]
    public string? NewName { get; set; }

    /// <summary>
    ///     The local URL to continue at once a passkey is registered, e.g. an authorization request that requires
    ///     multi-factor authentication. Other URLs are ignored.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     The <see cref="ReturnUrl"/> if it is local, otherwise null.
    /// </summary>
    public string? SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    /// <summary>
    ///     Where to continue: the <see cref="SafeReturnUrl"/>, otherwise the account overview.
    /// </summary>
    public string ContinueUrl => SafeReturnUrl ?? SecurityModel.PagePath;

    /// <summary>
    ///     The step-up that adds a second factor to the Session and returns here.
    /// </summary>
    public string StepUpUrl => $"{StepUpModel.PagePath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl is null ? PagePath : $"{PagePath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl)}")}";

    /// <summary>
    ///     The passkeys of the User, oldest first.
    /// </summary>
    public IReadOnlyList<PasskeyResponse> Passkeys { get; private set; } = [];

    /// <summary>
    ///     Whether the User must complete a second factor in the Session before changing their passkeys.
    /// </summary>
    public bool NeedsStepUp { get; private set; }

    /// <summary>
    ///     Whether the User can register another passkey.
    /// </summary>
    public bool CanRegister => Passkeys.Count < Passkey.MaxPerUser;

    /// <summary>
    ///     The confirmation to show, or null.
    /// </summary>
    public string? StatusMessage { get; private set; }

    /// <summary>
    ///     The error to show, or null.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    ///     Shows the passkeys and the form for a new one.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await ShowAsync(cancellationToken);
    }

    /// <summary>
    ///     Creates the options for <c>navigator.credentials.create</c> with a new challenge, called by the script.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>
    ///     The options as JSON; 409 Conflict if the User must step up first or has the maximum number of passkeys; 404
    ///     Not Found if the Session ended.
    /// </returns>
    public async Task<IActionResult> OnPostRegistrationOptionsAsync(CancellationToken cancellationToken)
    {
        var result = await startPasskeyRegistrationService.ExecuteAsync(GetUserId(), GetSessionId(), cancellationToken);

        return result.Status switch
        {
            StartPasskeyRegistrationStatus.Started => Content(result.OptionsJson!, "application/json"),
            StartPasskeyRegistrationStatus.SessionEnded => NotFound(),
            _ => StatusCode(StatusCodes.Status409Conflict)
        };
    }

    /// <summary>
    ///     Verifies the response of the browser and registers the passkey.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, the step-up, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostRegisterAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FailedAsync(ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage, cancellationToken);
        }

        RegisterPasskeyResult result;

        try
        {
            result = await registerPasskeyService.ExecuteAsync(
                new RegisterPasskeyRequest(GetUserId(), GetSessionId(), Name, Credential),
                cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return await FailedAsync(exception.Message, cancellationToken);
        }
        catch (ConflictException)
        {
            return await FailedAsync(ConflictMessage, cancellationToken);
        }

        return result switch
        {
            RegisterPasskeyResult.Registered => await SucceededAsync($"Passkey \"{Name?.Trim()}\" added. You can use it after your password from now on.", cancellationToken),
            RegisterPasskeyResult.AlreadyRegistered => await FailedAsync(AlreadyRegisteredMessage, cancellationToken),
            RegisterPasskeyResult.LimitReached => await FailedAsync(LimitReachedMessage, cancellationToken),
            RegisterPasskeyResult.StepUpRequired => LocalRedirect(StepUpUrl),
            RegisterPasskeyResult.SessionEnded => SessionEnded(),
            _ => await FailedAsync(RejectedMessage, cancellationToken)
        };
    }

    /// <summary>
    ///     Renames a passkey.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, the step-up, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostRenameAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FailedAsync(ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage, cancellationToken);
        }

        RenamePasskeyResult result;

        try
        {
            result = await renamePasskeyService.ExecuteAsync(GetUserId(), GetSessionId(), PasskeyId, NewName, cancellationToken);
        }
        catch (DomainValidationException exception)
        {
            return await FailedAsync(exception.Message, cancellationToken);
        }

        return result switch
        {
            RenamePasskeyResult.Renamed => await SucceededAsync("Passkey renamed.", cancellationToken),
            RenamePasskeyResult.StepUpRequired => LocalRedirect(StepUpUrl),
            RenamePasskeyResult.SessionEnded => SessionEnded(),
            _ => await FailedAsync("The passkey no longer exists.", cancellationToken)
        };
    }

    /// <summary>
    ///     Removes a passkey.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The page with a confirmation or an error, the step-up, or the sign-in page if the Session ended.</returns>
    public async Task<IActionResult> OnPostRemoveAsync(CancellationToken cancellationToken)
    {
        RemovePasskeyResult result;

        try
        {
            result = await removePasskeyService.ExecuteAsync(GetUserId(), GetSessionId(), PasskeyId, cancellationToken);
        }
        catch (ConflictException)
        {
            return await FailedAsync(ConflictMessage, cancellationToken);
        }

        return result switch
        {
            RemovePasskeyResult.Removed => await SucceededAsync("Passkey removed. You were signed out everywhere else.", cancellationToken),
            RemovePasskeyResult.StepUpRequired => LocalRedirect(StepUpUrl),
            RemovePasskeyResult.SessionEnded => SessionEnded(),
            _ => await FailedAsync("The passkey no longer exists.", cancellationToken)
        };
    }

    private async Task<IActionResult> ShowAsync(CancellationToken cancellationToken)
    {
        var (user, _, needsStepUp) = await guard.CheckAsync(GetUserId(), GetSessionId(), cancellationToken);

        if (user is null)
        {
            return SessionEnded();
        }

        NeedsStepUp = needsStepUp;
        Passkeys = (await getMfaStatusService.ExecuteAsync(user.Id, cancellationToken)).Passkeys;

        return Page();
    }

    private async Task<IActionResult> SucceededAsync(string message, CancellationToken cancellationToken)
    {
        ClearInput();
        StatusMessage = message;

        return await ShowAsync(cancellationToken);
    }

    private async Task<IActionResult> FailedAsync(string message, CancellationToken cancellationToken)
    {
        ClearInput();
        ErrorMessage = message;

        return await ShowAsync(cancellationToken);
    }

    private RedirectResult SessionEnded()
    {
        SessionCookie.Delete(Response);

        return Redirect($"{Request.PathBase}{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString($"{Request.PathBase}{PagePath}")}");
    }

    private Guid GetUserId()
    {
        // Always set by SessionAuthenticationHandler, the only scheme that signs Users in here.
        return Guid.Parse(User.FindFirst(SessionClaimTypes.Subject)!.Value);
    }

    private Guid GetSessionId()
    {
        return Guid.Parse(User.FindFirst(SessionClaimTypes.SessionId)!.Value);
    }

    private void ClearInput()
    {
        // The input helpers prefer the posted values, so they are removed from the ModelState as well. The return URL
        // is kept.
        Name = null;
        Credential = null;
        NewName = null;
        ModelState.Clear();
    }
}
