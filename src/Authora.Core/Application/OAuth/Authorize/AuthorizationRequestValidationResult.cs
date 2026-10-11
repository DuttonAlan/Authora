// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The result of validating an authorization request: either a valid request, or an error.
/// </summary>
/// <remarks>
///     An error can only be sent to the Client if the Client and the Redirect URI were validated
///     (<see cref="ErrorRedirectUri"/> is set). Otherwise, it must be shown to the User instead, so an attacker
///     cannot use Authora to redirect Users to arbitrary sites (RFC 6749, Section 4.1.2.1).
/// </remarks>
public sealed class AuthorizationRequestValidationResult
{
    private AuthorizationRequestValidationResult()
    {
    }

    /// <summary>
    ///     The valid request, or null if the request is invalid.
    /// </summary>
    public ValidatedAuthorizationRequest? Request { get; private init; }

    /// <summary>
    ///     The error code, see <see cref="OAuthErrors"/>, or null if the request is valid.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    ///     The description of the error, or null if the request is valid.
    /// </summary>
    public string? ErrorDescription { get; private init; }

    /// <summary>
    ///     The validated Redirect URI the error may be sent to, or null if the error must not be redirected.
    /// </summary>
    public string? ErrorRedirectUri { get; private init; }

    /// <summary>
    ///     The <c>state</c> to return with a redirected error, or null.
    /// </summary>
    public string? State { get; private init; }

    /// <summary>
    ///     Whether the request is valid.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Request))]
    [MemberNotNullWhen(false, nameof(Error), nameof(ErrorDescription))]
    public bool IsValid => Request is not null;

    /// <summary>
    ///     Creates the result of a valid request.
    /// </summary>
    /// <param name="request">The valid request.</param>
    /// <returns>The result.</returns>
    public static AuthorizationRequestValidationResult Valid(ValidatedAuthorizationRequest request)
    {
        return new AuthorizationRequestValidationResult { Request = request };
    }

    /// <summary>
    ///     Creates the result of a request whose error must be shown to the User, because the Client or the
    ///     Redirect URI is invalid.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <param name="description">The description of the error.</param>
    /// <returns>The result.</returns>
    public static AuthorizationRequestValidationResult NotRedirectable(string error, string description)
    {
        return new AuthorizationRequestValidationResult { Error = error, ErrorDescription = description };
    }

    /// <summary>
    ///     Creates the result of a request whose error is sent to the validated Redirect URI of the Client.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <param name="description">The description of the error.</param>
    /// <param name="redirectUri">The validated Redirect URI.</param>
    /// <param name="state">The <c>state</c> of the request, or null.</param>
    /// <returns>The result.</returns>
    public static AuthorizationRequestValidationResult Redirectable(string error, string description, string redirectUri, string? state)
    {
        return new AuthorizationRequestValidationResult
        {
            Error = error,
            ErrorDescription = description,
            ErrorRedirectUri = redirectUri,
            State = state
        };
    }
}
