// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth.ClientAuthentication;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.OAuth.Introspect;

/// <summary>
///     The Service behind the introspection endpoint (RFC 7662), which resource servers use to validate tokens.
/// </summary>
/// <remarks>
///     Only enabled Confidential Clients may introspect, so a resource server is registered as a Confidential Client.
///     Access Tokens can be introspected by every such Client. Refresh Tokens only by the Client they were issued
///     to, because they are never sent to resource servers.
/// </remarks>
/// <param name="clientAuthenticator">The authenticator for the calling Client.</param>
/// <param name="accessTokenValidator">The validator for Access Tokens.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to load Refresh Tokens.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check the User of a Refresh Token.</param>
/// <param name="secretHasher">The hasher to look up tokens.</param>
/// <param name="timeProvider">The TimeProvider for the expiry check.</param>
/// <param name="options">The OAuth settings with the Issuer.</param>
public sealed class IntrospectTokenService(
    ClientAuthenticator clientAuthenticator,
    AccessTokenValidator accessTokenValidator,
    ITokenRepository tokenRepository,
    IUserRepository userRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<OAuthOptions> options)
{
    /// <summary>
    ///     Authenticates the calling Client and returns whether the token is active.
    /// </summary>
    /// <param name="credentials">The credentials of the calling Client.</param>
    /// <param name="token">The token to introspect.</param>
    /// <param name="tokenTypeHint">The optional <c>token_type_hint</c>, see <see cref="TokenTypeHints"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The introspection response.</returns>
    /// <exception cref="OAuthException">Thrown when the Client authentication fails or the token is missing.</exception>
    public async Task<IntrospectionResponse> ExecuteAsync(
        ClientCredentials credentials,
        string? token,
        string? tokenTypeHint,
        CancellationToken cancellationToken = default)
    {
        var client = await clientAuthenticator.AuthenticateAsync(credentials, cancellationToken);

        // Public Clients cannot authenticate, so allowing them would let anyone check tokens.
        if (client.Type != ClientType.Confidential || client.Status == ClientStatus.Disabled)
        {
            throw new OAuthException(OAuthErrors.InvalidClient, "Only enabled Confidential Clients can introspect tokens.");
        }

        if (string.IsNullOrEmpty(token))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The token parameter is required.");
        }

        if (tokenTypeHint == TokenTypeHints.RefreshToken)
        {
            return await IntrospectRefreshTokenAsync(client.Id, token, cancellationToken)
                ?? await IntrospectAccessTokenAsync(token, cancellationToken)
                ?? IntrospectionResponse.Inactive;
        }

        return await IntrospectAccessTokenAsync(token, cancellationToken)
            ?? await IntrospectRefreshTokenAsync(client.Id, token, cancellationToken)
            ?? IntrospectionResponse.Inactive;
    }

    private async Task<IntrospectionResponse?> IntrospectAccessTokenAsync(string token, CancellationToken cancellationToken)
    {
        var accessToken = await accessTokenValidator.ValidateAsync(token, cancellationToken);

        if (accessToken is null)
        {
            return null;
        }

        return new IntrospectionResponse(
            Active: true,
            Scope: string.Join(' ', accessToken.Scopes),
            ClientId: accessToken.ClientId.ToString(),
            Subject: accessToken.UserId.ToString(),
            TokenType: Token.TokenResponse.BearerTokenType,
            ExpiresAt: ToUnixTime(accessToken.ExpiresAt),
            IssuedAt: ToUnixTime(accessToken.IssuedAt),
            Issuer: options.Value.Issuer);
    }

    private async Task<IntrospectionResponse?> IntrospectRefreshTokenAsync(Guid callerId, string token, CancellationToken cancellationToken)
    {
        var refreshToken = await tokenRepository.GetRefreshTokenByHash(secretHasher.Hash(token), cancellationToken);

        if (refreshToken is null
            || refreshToken.Grant!.ClientId != callerId
            || refreshToken.Grant.IsRevoked
            || refreshToken.ConsumedAt is not null
            || refreshToken.IsExpired(timeProvider.GetUtcNow().UtcDateTime))
        {
            return null;
        }

        var grant = refreshToken.Grant;
        var user = await userRepository.GetUserById(grant.UserId, cancellationToken);

        if (!TokenUserGuard.CanUse(user, grant.SecurityStamp))
        {
            return null;
        }

        return new IntrospectionResponse(
            Active: true,
            Scope: grant.Scope,
            ClientId: grant.ClientId.ToString(),
            Subject: grant.UserId.ToString(),
            ExpiresAt: ToUnixTime(refreshToken.ExpiresAt),
            IssuedAt: ToUnixTime(refreshToken.CreatedAt),
            Issuer: options.Value.Issuer);
    }

    private static long ToUnixTime(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }
}
