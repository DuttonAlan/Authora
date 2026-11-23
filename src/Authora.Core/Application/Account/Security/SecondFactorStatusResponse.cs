// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.Security;

/// <summary>
///     A second factor and whether the User enrolled in it.
/// </summary>
/// <param name="Method">The Authentication Method Reference value (RFC 8176), e.g. <c>otp</c>.</param>
/// <param name="DisplayName">The name shown to the User.</param>
/// <param name="IsEnrolled">Whether the User enrolled in it.</param>
/// <param name="Methods">Every method the Session may record for it, see <see cref="SecondFactors.ISecondFactor.Methods"/>.</param>
public sealed record SecondFactorStatusResponse(string Method, string DisplayName, bool IsEnrolled, IReadOnlyList<string> Methods);
