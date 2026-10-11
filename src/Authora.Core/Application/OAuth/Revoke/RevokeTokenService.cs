// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth.ClientAuthentication;
using Authora.Core.Domain.ValueObjects;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth.Revoke;

/// <summary>
///     The Service behind the revocation endpoint (RFC 7009).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>A Client can only revoke its own tokens. Tokens of other Clients are left unchanged.</item>
///         <item>Revoking a Refresh Token revokes its whole Grant, including all Access Tokens issued from it.</item>
///         <item>Revoking an Access Token only revokes this token.</item>
///         <item>
///             Unknown, invalid and foreign tokens are answered like a successful revocation (RFC 7009, Section 2.2),
///             so the endpoint does not reveal whether a token exists.
///         </item>
///         <item>Disabled Clients can still revoke their tokens.</item>
///     </list>
/// </remarks>
/// <param name="clientAuthenticator">The authenticator for the calling Client.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to find and revoke tokens.</param>
/// <param name="secretHasher">The hasher to look up tokens.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="logger">The Logger to record revocations.</param>
public sealed class RevokeTokenService(
    ClientAuthenticator clientAuthenticator,
    ITokenRepository tokenRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<RevokeTokenService> logger)
{
    /// <summary>
    ///     Authenticates the calling Client and revokes the token if it belongs to the Client.
    /// </summary>
    /// <param name="credentials">The credentials of the calling Client.</param>
    /// <param name="token">The token to revoke.</param>
    /// <param name="tokenTypeHint">The optional <c>token_type_hint</c>, see <see cref="TokenTypeHints"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the request is handled.</returns>
    /// <exception cref="OAuthException">Thrown when the Client authentication fails or the token is missing.</exception>
    public async Task ExecuteAsync(
        ClientCredentials credentials,
        string? token,
        string? tokenTypeHint,
        CancellationToken cancellationToken = default)
    {
        var client = await clientAuthenticator.AuthenticateAsync(credentials, cancellationToken);

        if (string.IsNullOrEmpty(token))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The token parameter is required.");
        }

        var tokenHash = secretHasher.Hash(token);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // The hint only decides which lookup comes first (RFC 7009, Section 2.1). Unknown hints are ignored.
        _ = tokenTypeHint == TokenTypeHints.RefreshToken
            ? await TryRevokeRefreshTokenAsync(client.Id, tokenHash, now, cancellationToken)
                || await TryRevokeAccessTokenAsync(client.Id, tokenHash, now, cancellationToken)
            : await TryRevokeAccessTokenAsync(client.Id, tokenHash, now, cancellationToken)
                || await TryRevokeRefreshTokenAsync(client.Id, tokenHash, now, cancellationToken);
    }

    private async Task<bool> TryRevokeRefreshTokenAsync(
        Guid clientId,
        SecretHash tokenHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var refreshToken = await tokenRepository.GetRefreshTokenByHash(tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return false;
        }

        if (refreshToken.Grant!.ClientId != clientId)
        {
            logger.TokenOfOtherClient(clientId, refreshToken.Grant.ClientId);
            return true;
        }

        await tokenRepository.RevokeGrant(refreshToken.GrantId, now, cancellationToken);
        logger.GrantRevokedByClient(refreshToken.GrantId, clientId);

        return true;
    }

    private async Task<bool> TryRevokeAccessTokenAsync(
        Guid clientId,
        SecretHash tokenHash,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenRepository.GetAccessTokenByHash(tokenHash, cancellationToken);

        if (accessToken is null)
        {
            return false;
        }

        if (accessToken.Grant!.ClientId != clientId)
        {
            logger.TokenOfOtherClient(clientId, accessToken.Grant.ClientId);
            return true;
        }

        await tokenRepository.RevokeAccessToken(accessToken.Id, now, cancellationToken);
        logger.AccessTokenRevoked(accessToken.Id, clientId);

        return true;
    }
}
