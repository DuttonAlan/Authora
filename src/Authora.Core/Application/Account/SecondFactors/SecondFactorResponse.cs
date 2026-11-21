// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.SecondFactors;

/// <summary>
///     A second factor the User can complete.
/// </summary>
/// <param name="Method">The Authentication Method Reference value (RFC 8176), e.g. <c>otp</c>.</param>
/// <param name="DisplayName">The name shown to the User.</param>
public sealed record SecondFactorResponse(string Method, string DisplayName);
