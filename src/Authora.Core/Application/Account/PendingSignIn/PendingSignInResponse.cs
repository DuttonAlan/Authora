// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.SecondFactors;

namespace Authora.Core.Application.Account.PendingSignIn;

/// <summary>
///     A pending sign-in that waits for a second factor.
/// </summary>
/// <param name="Email">Email of the User who is signing in.</param>
/// <param name="IsStepUp">Whether it is a step-up of the current Session instead of a new sign-in.</param>
/// <param name="Factors">The second factors the User can complete, at least one.</param>
/// <param name="ExpiresAt">Point in time (UTC) when it expires.</param>
public sealed record PendingSignInResponse(
    string Email,
    bool IsStepUp,
    IReadOnlyList<SecondFactorResponse> Factors,
    DateTime ExpiresAt);
