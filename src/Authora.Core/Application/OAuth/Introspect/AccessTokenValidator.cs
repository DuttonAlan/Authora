// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.OAuth.Introspect;

/// <summary>
///     Validates Access Tokens, e.g. for the introspection endpoint.
/// </summary>
/// <remarks>
///     A token is only active if all of these hold: it exists, is not expired or revoked, its Grant is not revoked,
///     its Client exists and is enabled, and its User, if it has one, can still sign in with the same Security Stamp.
/// </remarks>
/// <param name="tokenRepository">An Instance of a ITokenRepository to load the token.</param>
/// <param name="clientRepository">An Instance of a IClientRepository to check the Client.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check the User.</param>
/// <param name="secretHasher">The hasher to look up the token.</param>
/// <param name="timeProvider">The TimeProvider for the expiry check.</param>
public sealed class AccessTokenValidator(
    ITokenRepository tokenRepository,
    IClientRepository clientRepository,
    IUserRepository userRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider)
{
    /// <summary>
    ///     Validates an Access Token.
    /// </summary>
    /// <param name="token">The plain-text Access Token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The details of the active token, or null if the token is not active.</returns>
    public async Task<ValidatedAccessToken?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var accessToken = await tokenRepository.GetAccessTokenByHash(secretHasher.Hash(token), cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (accessToken is null || !accessToken.IsActive(now) || accessToken.Grant!.IsRevoked || accessToken.Grant.IsExpired(now))
        {
            return null;
        }

        var grant = accessToken.Grant;

        var client = await clientRepository.GetClientById(grant.ClientId, cancellationToken);

        if (client is null || client.Status == ClientStatus.Disabled)
        {
            return null;
        }

        // A token of the Client itself (client_credentials) has no User to check.
        if (grant.UserId is { } userId)
        {
            var user = await userRepository.GetUserById(userId, cancellationToken);

            if (!TokenUserGuard.CanUse(user, grant.SecurityStamp))
            {
                return null;
            }
        }

        return new ValidatedAccessToken(
            accessToken.Id,
            grant.Id,
            grant.ClientId,
            grant.UserId,
            accessToken.Scopes,
            accessToken.CreatedAt,
            accessToken.ExpiresAt,
            grant.AuthenticationMethods,
            accessToken.Audience);
    }
}

/// <summary>
///     The details of an active Access Token.
/// </summary>
/// <param name="Id">Id of the Access Token.</param>
/// <param name="GrantId">Id of the Grant the token belongs to.</param>
/// <param name="ClientId">Id of the Client the token was issued to.</param>
/// <param name="UserId">
///     Id of the User, used as <c>sub</c>, or null for a token the Client obtained for itself (client_credentials).
/// </param>
/// <param name="Scopes">The Scope Names of the token.</param>
/// <param name="IssuedAt">Point in time (UTC) when the token was issued.</param>
/// <param name="ExpiresAt">Point in time (UTC) when the token expires.</param>
/// <param name="AuthenticationMethods">
///     The methods the User completed in the Session the Grant was approved in, e.g. <c>pwd</c> and <c>otp</c>. Empty
///     for a token of the Client itself and for Grants from before the methods were recorded.
/// </param>
/// <param name="Audience">
///     The Ids of the Clients the token is meant for, and <see cref="Domain.AuthoraResource.Id"/> if it is meant for
///     Authora itself (RFC 8707).
/// </param>
public sealed record ValidatedAccessToken(
    Guid Id,
    Guid GrantId,
    Guid ClientId,
    Guid? UserId,
    IReadOnlyList<string> Scopes,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    IReadOnlyList<string> AuthenticationMethods,
    IReadOnlyList<Guid> Audience)
{
    /// <summary>
    ///     The <c>sub</c> of the token: the User, or the Client if the token has no User.
    /// </summary>
    public string Subject => (UserId ?? ClientId).ToString();

    /// <summary>
    ///     Whether the token is meant for Authora itself (admin API, UserInfo), i.e. Authora is in its audience.
    /// </summary>
    public bool IsMeantForAuthora => Audience.Contains(Domain.AuthoraResource.Id);
}
