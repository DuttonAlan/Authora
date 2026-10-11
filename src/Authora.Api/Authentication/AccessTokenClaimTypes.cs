// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.Authentication;

/// <summary>
///     Claim types of the principal created by <see cref="AccessTokenAuthenticationHandler"/>.
/// </summary>
internal static class AccessTokenClaimTypes
{
    /// <summary>
    ///     Id of the User.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    ///     Id of the Client the token was issued to.
    /// </summary>
    public const string ClientId = "client_id";

    /// <summary>
    ///     One Scope of the token. There is one claim per Scope.
    /// </summary>
    public const string Scope = "scope";
}
