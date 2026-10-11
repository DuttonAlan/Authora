// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Values of the <c>token_type_hint</c> parameter of the revocation and introspection endpoints (RFC 7009, Section 2.1).
/// </summary>
public static class TokenTypeHints
{
    /// <summary>
    ///     The token is probably an Access Token.
    /// </summary>
    public const string AccessToken = "access_token";

    /// <summary>
    ///     The token is probably a Refresh Token.
    /// </summary>
    public const string RefreshToken = "refresh_token";
}
