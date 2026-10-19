// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     Creates ID Tokens (OpenID Connect Core 1.0, Section 2), which tell the Client who signed in.
/// </summary>
/// <remarks>
///     ID Tokens are signed JWTs for the Client only (<c>aud</c> is its <c>client_id</c>). They are never stored and
///     cannot be used as Access Tokens: APIs, the UserInfo endpoint and introspection only accept the opaque Access
///     Tokens, which are looked up by their hash.
/// </remarks>
/// <param name="signingKeyProvider">The provider of the signing key.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer.</param>
/// <param name="oidcOptions">The OpenID Connect settings with the lifetime.</param>
public sealed class IdTokenIssuer(
    ISigningKeyProvider signingKeyProvider,
    IOptions<OAuthOptions> oauthOptions,
    IOptions<OidcOptions> oidcOptions)
{
    /// <summary>
    ///     Creates an ID Token for an exchanged Authorization Code.
    /// </summary>
    /// <param name="user">The User who signed in.</param>
    /// <param name="authorizationCode">The exchanged code, with the <c>nonce</c> and the time of the sign-in.</param>
    /// <param name="scopes">The Scope Names of the issued Access Token. They decide which identity claims are added.</param>
    /// <param name="accessToken">The plain-text Access Token issued together with the ID Token, for <c>at_hash</c>.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The signed ID Token.</returns>
    public string Issue(
        User user,
        AuthorizationCode authorizationCode,
        IReadOnlyCollection<string> scopes,
        string accessToken,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(authorizationCode);
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        var claims = IdentityClaims.Get(user, scopes);

        claims["iss"] = oauthOptions.Value.Issuer;
        claims["aud"] = authorizationCode.ClientId.ToString("D");
        claims["iat"] = ToUnixTime(now);
        claims["exp"] = ToUnixTime(now + oidcOptions.Value.IdTokenLifetime);

        // Always sent, so Clients can enforce max_age themselves, also after Authora skipped it on the way back from
        // the sign-in page.
        claims["auth_time"] = ToUnixTime(authorizationCode.AuthenticatedAt);
        claims["at_hash"] = ComputeAccessTokenHash(accessToken);

        // Returned unchanged, so the Client can check that the token belongs to its request (replay protection).
        if (authorizationCode.Nonce is not null)
        {
            claims["nonce"] = authorizationCode.Nonce;
        }

        return JsonWebToken.Create(claims, signingKeyProvider.SigningKey);
    }

    // OpenID Connect Core 1.0, Section 3.1.3.6: the left half of the SHA-256 hash (for RS256) of the ASCII Access Token.
    private static string ComputeAccessTokenHash(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));

        return Base64Url.EncodeToString(hash.AsSpan(0, hash.Length / 2));
    }

    private static long ToUnixTime(DateTime value) => new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds();
}
