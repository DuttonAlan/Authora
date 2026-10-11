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
///     its Client exists and is enabled, and its User can still sign in with the same Security Stamp.
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

        if (accessToken is null || !accessToken.IsActive(timeProvider.GetUtcNow().UtcDateTime) || accessToken.Grant!.IsRevoked)
        {
            return null;
        }

        var grant = accessToken.Grant;

        var client = await clientRepository.GetClientById(grant.ClientId, cancellationToken);

        if (client is null || client.Status == ClientStatus.Disabled)
        {
            return null;
        }

        var user = await userRepository.GetUserById(grant.UserId, cancellationToken);

        if (!TokenUserGuard.CanUse(user, grant.SecurityStamp))
        {
            return null;
        }

        return new ValidatedAccessToken(
            accessToken.Id,
            grant.Id,
            grant.ClientId,
            grant.UserId,
            accessToken.Scopes,
            accessToken.CreatedAt,
            accessToken.ExpiresAt);
    }
}

/// <summary>
///     The details of an active Access Token.
/// </summary>
/// <param name="Id">Id of the Access Token.</param>
/// <param name="GrantId">Id of the Grant the token belongs to.</param>
/// <param name="ClientId">Id of the Client the token was issued to.</param>
/// <param name="UserId">Id of the User, used as <c>sub</c>.</param>
/// <param name="Scopes">The Scope Names of the token.</param>
/// <param name="IssuedAt">Point in time (UTC) when the token was issued.</param>
/// <param name="ExpiresAt">Point in time (UTC) when the token expires.</param>
public sealed record ValidatedAccessToken(
    Guid Id,
    Guid GrantId,
    Guid ClientId,
    Guid UserId,
    IReadOnlyList<string> Scopes,
    DateTime IssuedAt,
    DateTime ExpiresAt);
