// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     The result of validating a logout request. An invalid request is shown as an error and never redirected
///     (OpenID Connect RP-Initiated Logout 1.0, Section 4), so it has no redirect target.
/// </summary>
public sealed class EndSessionRequestValidationResult
{
    private EndSessionRequestValidationResult(ValidatedEndSessionRequest? request, string? errorDescription)
    {
        Request = request;
        ErrorDescription = errorDescription;
    }

    /// <summary>
    ///     Whether the request is valid.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Request))]
    [MemberNotNullWhen(false, nameof(ErrorDescription))]
    public bool IsValid => Request is not null;

    /// <summary>
    ///     The valid request, or null.
    /// </summary>
    public ValidatedEndSessionRequest? Request { get; }

    /// <summary>
    ///     Why the request is invalid, shown to the User, or null.
    /// </summary>
    public string? ErrorDescription { get; }

    /// <summary>
    ///     Creates the result for a valid request.
    /// </summary>
    /// <param name="request">The valid request.</param>
    /// <returns>The result.</returns>
    public static EndSessionRequestValidationResult Valid(ValidatedEndSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new EndSessionRequestValidationResult(request, null);
    }

    /// <summary>
    ///     Creates the result for an invalid request.
    /// </summary>
    /// <param name="errorDescription">Why the request is invalid.</param>
    /// <returns>The result.</returns>
    public static EndSessionRequestValidationResult Invalid(string errorDescription)
    {
        ArgumentException.ThrowIfNullOrEmpty(errorDescription);

        return new EndSessionRequestValidationResult(null, errorDescription);
    }
}
