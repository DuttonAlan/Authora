// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.OAuth.ClientAuthentication;
using Authora.Core.Application.Oidc;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth.Token;

/// <summary>
///     The Service behind the /token endpoint. Exchanges an Authorization Code or a Refresh Token for new tokens.
/// </summary>
/// <param name="clientAuthenticator">The authenticator for the Client.</param>
/// <param name="authorizationCodeRepository">An Instance of a IAuthorizationCodeRepository to load and consume codes.</param>
/// <param name="clientRepository">An Instance of a IClientRepository to check whether the Client was disabled meanwhile.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to store, consume and revoke tokens.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check the User.</param>
/// <param name="tokenIssuer">The issuer for new tokens.</param>
/// <param name="idTokenIssuer">The issuer for ID Tokens.</param>
/// <param name="secretHasher">The hasher to look up codes and tokens.</param>
/// <param name="timeProvider">The TimeProvider for expiry checks and new tokens.</param>
/// <param name="auditLog">The audit log to record attacks.</param>
/// <param name="logger">The Logger to record issued tokens and attacks.</param>
public sealed class IssueTokenService(
    ClientAuthenticator clientAuthenticator,
    IAuthorizationCodeRepository authorizationCodeRepository,
    IClientRepository clientRepository,
    ITokenRepository tokenRepository,
    IUserRepository userRepository,
    TokenIssuer tokenIssuer,
    IdTokenIssuer idTokenIssuer,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<IssueTokenService> logger)
{
    /// <summary>
    ///     Authenticates the Client and exchanges the code or Refresh Token for new tokens.
    /// </summary>
    /// <param name="request">The parameters of the token request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new tokens.</returns>
    /// <exception cref="OAuthException">Thrown when the request must be answered with an OAuth error.</exception>
    public async Task<TokenResponse> ExecuteAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.GrantType))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The grant_type parameter is required.");
        }

        if (!GrantTypeNames.TryParse(request.GrantType, out var grantType))
        {
            throw new OAuthException(OAuthErrors.UnsupportedGrantType, "Only authorization_code and refresh_token are supported.");
        }

        var client = await clientAuthenticator.AuthenticateAsync(request.Credentials, cancellationToken);

        // A disabled Client cannot obtain new tokens (US-014), not even with a Refresh Token (TD-002).
        if (client.Status == ClientStatus.Disabled)
        {
            throw new OAuthException(OAuthErrors.InvalidClient, "The Client is disabled.");
        }

        if (!client.IsGrantTypeAllowed(grantType))
        {
            throw new OAuthException(OAuthErrors.UnauthorizedClient, "The Client is not allowed to use this grant_type.");
        }

        return grantType == GrantTypes.AuthorizationCode
            ? await ExchangeAuthorizationCodeAsync(client, request, cancellationToken)
            : await ExchangeRefreshTokenAsync(client, request, cancellationToken);
    }

    private async Task<TokenResponse> ExchangeAuthorizationCodeAsync(
        Client client,
        TokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The code parameter is required.");
        }

        if (string.IsNullOrEmpty(request.CodeVerifier))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "PKCE is required: the code_verifier parameter is missing.");
        }

        if (!Pkce.IsValidCodeVerifier(request.CodeVerifier))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The code_verifier must be 43 to 128 unreserved characters.");
        }

        var invalidCode = new OAuthException(OAuthErrors.InvalidGrant, "The Authorization Code is invalid, expired or already used.");

        var authorizationCode = await authorizationCodeRepository.GetAuthorizationCodeByHash(
            secretHasher.Hash(request.Code),
            cancellationToken) ?? throw invalidCode;

        if (authorizationCode.ClientId != client.Id)
        {
            logger.TokenOfOtherClient(client.Id, authorizationCode.ClientId);
            throw invalidCode;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Consume first, so the code is single-use no matter which of the following checks fails.
        if (!await authorizationCodeRepository.TryConsume(authorizationCode.Id, now, cancellationToken))
        {
            // OAuth 2.1, Section 4.1.3: a code used twice was probably stolen, so revoke the tokens issued from it.
            // Mark first: the request that consumed the code may not have saved its Grant yet, and checks the mark after saving.
            await authorizationCodeRepository.MarkReused(authorizationCode.Id, now, cancellationToken);
            var revoked = await tokenRepository.RevokeGrantsOfAuthorizationCode(authorizationCode.Id, now, cancellationToken);
            logger.AuthorizationCodeReused(authorizationCode.Id, revoked);
            await RecordCodeReusedAsync(client, authorizationCode, revoked, cancellationToken);

            throw invalidCode;
        }

        if (authorizationCode.IsExpired(now))
        {
            throw invalidCode;
        }

        if (authorizationCode.RedirectUri is not null
            && !string.Equals(authorizationCode.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
        {
            throw new OAuthException(OAuthErrors.InvalidGrant, "The redirect_uri does not match the authorization request.");
        }

        if (!Pkce.Verify(request.CodeVerifier, authorizationCode.CodeChallenge))
        {
            throw new OAuthException(OAuthErrors.InvalidGrant, "The code_verifier does not match the code_challenge.");
        }

        var user = await userRepository.GetUserById(authorizationCode.UserId, cancellationToken);

        if (!TokenUserGuard.CanUse(user, authorizationCode.SecurityStamp))
        {
            throw invalidCode;
        }

        var scopes = GetScopesStillAllowed(client, authorizationCode.Scopes);

        var grant = Grant.Create(authorizationCode, now);
        var tokens = tokenIssuer.Issue(grant.Id, client, scopes, now);

        await tokenRepository.CreateGrant(grant, tokens.AccessToken, tokens.RefreshToken, cancellationToken);

        // A reuse detected while this request was running found no Grant to revoke, so revoke it here.
        if (await authorizationCodeRepository.IsReused(authorizationCode.Id, cancellationToken))
        {
            await tokenRepository.RevokeGrant(grant.Id, now, cancellationToken);
            logger.AuthorizationCodeReused(authorizationCode.Id, 1);
            await RecordCodeReusedAsync(client, authorizationCode, 1, cancellationToken);

            throw invalidCode;
        }

        // The Client may have been disabled after the status check above. Disabling revokes only existing Grants,
        // so a Grant saved afterwards must be revoked here, or it would become usable when the Client is enabled again.
        if (await clientRepository.IsClientDisabled(client.Id, cancellationToken))
        {
            await tokenRepository.RevokeGrant(grant.Id, now, cancellationToken);
            logger.GrantRevokedForDisabledClient(grant.Id, client.Id);

            throw new OAuthException(OAuthErrors.InvalidClient, "The Client is disabled.");
        }

        logger.GrantCreated(grant.Id, client.Id, grant.UserId, authorizationCode.Id);

        // OpenID Connect Core 1.0, Section 3.1.3.3: an ID Token only for requests with openid that the Client may
        // still use. Not on refresh, which OpenID Connect leaves optional.
        if (!scopes.Contains(OidcScopes.OpenId))
        {
            return tokens.Response;
        }

        return tokens.Response with
        {
            IdToken = idTokenIssuer.Issue(user, authorizationCode, scopes, tokens.Response.AccessToken, now)
        };
    }

    private async Task<TokenResponse> ExchangeRefreshTokenAsync(
        Client client,
        TokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            throw new OAuthException(OAuthErrors.InvalidRequest, "The refresh_token parameter is required.");
        }

        var invalidToken = new OAuthException(OAuthErrors.InvalidGrant, "The Refresh Token is invalid, expired or revoked.");

        var refreshToken = await tokenRepository.GetRefreshTokenByHash(
            secretHasher.Hash(request.RefreshToken),
            cancellationToken) ?? throw invalidToken;

        var grant = refreshToken.Grant!;

        if (grant.ClientId != client.Id)
        {
            logger.TokenOfOtherClient(client.Id, grant.ClientId);
            throw invalidToken;
        }

        if (grant.IsRevoked)
        {
            throw invalidToken;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (refreshToken.ConsumedAt is not null)
        {
            await RevokeReusedGrantAsync(grant, refreshToken, now, cancellationToken);
            throw invalidToken;
        }

        if (refreshToken.IsExpired(now))
        {
            throw invalidToken;
        }

        var scopes = grant.Scopes;

        // RFC 6749, Section 6: the Client may request fewer Scopes, but never more than were approved.
        if (request.Scope is not null)
        {
            if (!ScopeList.TryParse(request.Scope, out var requested) || !requested.All(scopes.Contains))
            {
                throw new OAuthException(OAuthErrors.InvalidScope, "The scope must not exceed the Scopes approved by the User.");
            }

            scopes = requested;
        }

        var accessTokenScopes = GetScopesStillAllowed(client, scopes);

        var user = await userRepository.GetUserById(grant.UserId, cancellationToken);

        if (!TokenUserGuard.CanUse(user, grant.SecurityStamp))
        {
            await tokenRepository.RevokeGrant(grant.Id, now, cancellationToken);
            logger.GrantRevokedForUser(grant.Id, grant.UserId);

            throw invalidToken;
        }

        // Concurrent requests with the same token: only one wins, the others count as reuse.
        if (!await tokenRepository.TryConsumeRefreshToken(refreshToken.Id, now, cancellationToken))
        {
            await RevokeReusedGrantAsync(grant, refreshToken, now, cancellationToken);
            throw invalidToken;
        }

        var tokens = tokenIssuer.Issue(grant.Id, client, accessTokenScopes, now);

        await tokenRepository.AddTokens(tokens.AccessToken, tokens.RefreshToken, cancellationToken);

        logger.TokensRefreshed(grant.Id, client.Id);

        return tokens.Response;
    }

    /// <summary>
    ///     OAuth 2.1, Section 4.3.1: a rotated Refresh Token that is used again was probably stolen, and the server
    ///     cannot tell whether the attacker or the Client used it first. So the whole Grant is revoked.
    /// </summary>
    private async Task RevokeReusedGrantAsync(
        Grant grant,
        RefreshToken refreshToken,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await tokenRepository.RevokeGrant(refreshToken.GrantId, now, cancellationToken);
        logger.RefreshTokenReused(refreshToken.Id, refreshToken.GrantId);

        await auditLog.RecordNowAsync(
            new AuditEntry(
                AuditEventTypes.OAuth.RefreshTokenReused,
                AuditTargetTypes.User,
                grant.UserId.ToString(),
                $"Refresh Token {refreshToken.Id}, Grant {grant.Id} revoked")
            {
                Outcome = AuditOutcome.Failed,
                ClientId = grant.ClientId
            },
            cancellationToken);
    }

    private async Task RecordCodeReusedAsync(
        Client client,
        AuthorizationCode authorizationCode,
        int revokedGrants,
        CancellationToken cancellationToken)
    {
        await auditLog.RecordNowAsync(
            new AuditEntry(
                AuditEventTypes.OAuth.AuthorizationCodeReused,
                AuditTargetTypes.User,
                authorizationCode.UserId.ToString(),
                $"Authorization Code {authorizationCode.Id}, {revokedGrants} Grant(s) revoked")
            {
                Outcome = AuditOutcome.Failed,
                ClientId = client.Id
            },
            cancellationToken);
    }

    /// <summary>
    ///     Removes Scopes that an administrator disallowed for the Client after the User approved them.
    /// </summary>
    private static IReadOnlyList<string> GetScopesStillAllowed(Client client, IReadOnlyList<string> scopes)
    {
        var allowed = scopes.Where(client.IsScopeAllowed).ToList();

        if (allowed.Count == 0)
        {
            throw new OAuthException(OAuthErrors.InvalidScope, "None of the approved Scopes is allowed for the Client anymore.");
        }

        return allowed;
    }
}
