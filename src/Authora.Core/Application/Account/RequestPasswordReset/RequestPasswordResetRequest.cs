// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account.RequestPasswordReset;

/// <summary>
///     Represents the request for a password reset link, entered on the sign-in page.
/// </summary>
/// <param name="Email">The address to send the link to, if an account that can sign in has it.</param>
public sealed record RequestPasswordResetRequest(string Email);
