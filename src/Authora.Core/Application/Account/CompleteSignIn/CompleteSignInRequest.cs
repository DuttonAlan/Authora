// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.CompleteSignIn;

/// <summary>
///     The second factor a User completes for a pending sign-in.
/// </summary>
/// <param name="ChallengeToken">The plain-text token of the pending sign-in from its cookie.</param>
/// <param name="Method">The method of the factor the User chose, e.g. <c>otp</c>.</param>
/// <param name="Response">The response the User entered, e.g. a code.</param>
public sealed record CompleteSignInRequest(string? ChallengeToken, string Method, string Response)
{
    /// <summary>
    ///     Returns a description without the token and the response, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"CompleteSignInRequest(Method = {Method})";
}
