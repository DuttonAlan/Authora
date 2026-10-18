// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.OAuth.Token;

/// <summary>
///     Generates new Access and Refresh Tokens for a Grant. The tokens still have to be saved.
/// </summary>
/// <param name="secretGenerator">The generator for the random tokens.</param>
/// <param name="secretHasher">The hasher for the stored tokens.</param>
public sealed class TokenIssuer(ISecretGenerator secretGenerator, ISecretHasher secretHasher)
{
    /// <summary>
    ///     Generates a new Access Token and, if the Client may use Refresh Tokens, a new Refresh Token.
    /// </summary>
    /// <param name="grant">The Grant the tokens belong to. No token expires after it.</param>
    /// <param name="client">The Client the tokens are issued to. Its TTLs are used.</param>
    /// <param name="scopes">The Scope Names of the Access Token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The new tokens and the response for the Client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Grant has expired, which the caller checks first.</exception>
    public IssuedTokens Issue(Grant grant, Client client, IReadOnlyList<string> scopes, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(client);

        if (grant.IsExpired(now))
        {
            throw new InvalidOperationException($"The Grant {grant.Id} has expired, so no tokens can be issued for it.");
        }

        var accessTokenLifetime = grant.LimitTokenLifetime(now, TimeSpan.FromSeconds(client.AccessTokenTTL));
        var accessTokenValue = secretGenerator.Generate();
        var accessToken = AccessToken.Create(
            grant.Id,
            secretHasher.Hash(accessTokenValue),
            scopes,
            now,
            accessTokenLifetime);

        string? refreshTokenValue = null;
        RefreshToken? refreshToken = null;

        if (client.IsGrantTypeAllowed(GrantTypes.RefreshToken))
        {
            refreshTokenValue = secretGenerator.Generate();
            refreshToken = RefreshToken.Create(
                grant.Id,
                secretHasher.Hash(refreshTokenValue),
                now,
                grant.LimitTokenLifetime(now, TimeSpan.FromSeconds(client.RefreshTokenTTL)));
        }

        var response = new TokenResponse(
            accessTokenValue,
            TokenResponse.BearerTokenType,
            (int)accessTokenLifetime.TotalSeconds,
            refreshTokenValue,
            ScopeList.Format(scopes));

        return new IssuedTokens(accessToken, refreshToken, response);
    }
}

/// <summary>
///     Tokens created by <see cref="TokenIssuer"/>.
/// </summary>
/// <param name="AccessToken">The new Access Token to save.</param>
/// <param name="RefreshToken">The new Refresh Token to save, or null.</param>
/// <param name="Response">The response with the plain-text tokens for the Client.</param>
public sealed record IssuedTokens(AccessToken AccessToken, RefreshToken? RefreshToken, TokenResponse Response);
