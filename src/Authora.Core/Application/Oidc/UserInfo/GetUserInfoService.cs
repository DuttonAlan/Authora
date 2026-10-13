// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Introspect;

namespace Authora.Core.Application.Oidc.UserInfo;

/// <summary>
///     The Service behind the UserInfo endpoint (OpenID Connect Core 1.0, Section 5.3, US-025). Returns the identity
///     claims of the User an Access Token was issued for.
/// </summary>
/// <remarks>
///     The token must be active (see <see cref="AccessTokenValidator"/>) and contain <see cref="OidcScopes.OpenId"/>.
///     Only the claims of the token's Scopes are returned, the same as in the ID Token, see
///     <see cref="IdentityClaims"/>. ID Tokens are not accepted, because only Access Tokens are looked up.
/// </remarks>
/// <param name="accessTokenValidator">The validator of the Access Token.</param>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
public sealed class GetUserInfoService(AccessTokenValidator accessTokenValidator, IUserRepository userRepository)
{
    /// <summary>
    ///     Returns the claims of the User of an Access Token.
    /// </summary>
    /// <param name="accessToken">The plain-text Access Token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The claims by name.</returns>
    /// <exception cref="OAuthException">
    ///     Thrown with <see cref="OAuthErrors.InvalidToken"/> when the token is not active, or with
    ///     <see cref="OAuthErrors.InsufficientScope"/> when it lacks <see cref="OidcScopes.OpenId"/>.
    /// </exception>
    public async Task<IReadOnlyDictionary<string, object>> ExecuteAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        var invalidToken = new OAuthException(OAuthErrors.InvalidToken, "The Access Token is invalid, expired or revoked.");

        var token = await accessTokenValidator.ValidateAsync(accessToken, cancellationToken) ?? throw invalidToken;

        if (!token.Scopes.Contains(OidcScopes.OpenId))
        {
            throw new OAuthException(OAuthErrors.InsufficientScope, $"The Access Token must have the Scope {OidcScopes.OpenId}.");
        }

        // The validator checked the User, so this only fails if the User was deleted in the meantime.
        var user = await userRepository.GetUserById(token.UserId, cancellationToken) ?? throw invalidToken;

        return IdentityClaims.Get(user, token.Scopes);
    }
}
