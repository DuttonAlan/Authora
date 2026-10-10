// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Defines the Grant Types a Client can use at the /token endpoint.
///     Values can be combined, e.g. <c>AuthorizationCode | RefreshToken</c>.
/// </summary>
[Flags]
public enum GrantTypes
{
    /// <summary>
    ///     No Grant Type is allowed.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Allows <c>grant_type=authorization_code</c> to exchange an Authorization Code for tokens.
    /// </summary>
    AuthorizationCode = 1,

    /// <summary>
    ///     Allows <c>grant_type=refresh_token</c> to exchange a Refresh Token for a new Access Token.
    /// </summary>
    RefreshToken = 2
}
