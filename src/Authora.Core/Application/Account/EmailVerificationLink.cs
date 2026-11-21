// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Account;

/// <summary>
///     Builds the links of email verification emails.
/// </summary>
public static class EmailVerificationLink
{
    /// <summary>
    ///     Path of the page where a User confirms their address with the token of a link.
    /// </summary>
    public const string PagePath = "/account/verify-email";

    /// <summary>
    ///     Name of the query parameter with the token.
    /// </summary>
    public const string TokenParameter = "token";

    /// <summary>
    ///     Builds the link to the verification page.
    /// </summary>
    /// <param name="issuer">The Issuer, see <see cref="OAuth.OAuthOptions.Issuer"/>, without a trailing slash.</param>
    /// <param name="token">The plain-text token of the link.</param>
    /// <returns>The absolute link.</returns>
    public static Uri Create(string issuer, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new Uri($"{issuer}{PagePath}?{TokenParameter}={Uri.EscapeDataString(token)}", UriKind.Absolute);
    }
}
