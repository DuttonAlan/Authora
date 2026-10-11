// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Grant, Access Token and Refresh Token operations.
/// </summary>
/// <remarks>
///     Consuming and revoking are executed as single atomic updates in the database, so they are safe against
///     concurrent requests and do not need a separate save.
/// </remarks>
public interface ITokenRepository
{
    /// <summary>
    ///     Create a new Grant together with its first tokens.
    /// </summary>
    /// <param name="grant">The Grant to create.</param>
    /// <param name="accessToken">The first Access Token of the Grant.</param>
    /// <param name="refreshToken">The first Refresh Token of the Grant, or null if the Client does not get one.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once everything is saved.</returns>
    public Task CreateGrant(Grant grant, AccessToken accessToken, RefreshToken? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Add new tokens to an existing Grant.
    /// </summary>
    /// <param name="accessToken">The new Access Token.</param>
    /// <param name="refreshToken">The new Refresh Token, or null.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the tokens are saved.</returns>
    public Task AddTokens(AccessToken accessToken, RefreshToken? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get an Access Token by the hash of its token, including its Grant. The token is not tracked.
    /// </summary>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Access Token, or null if no Access Token has this value.</returns>
    public Task<AccessToken?> GetAccessTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Refresh Token by the hash of its token, including its Grant. The token is not tracked.
    /// </summary>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Refresh Token, or null if no Refresh Token has this value.</returns>
    public Task<RefreshToken?> GetRefreshTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a Refresh Token as consumed in a single atomic update, unless it was already consumed.
    /// </summary>
    /// <remarks>
    ///     Of several concurrent requests with the same token, exactly one succeeds.
    /// </remarks>
    /// <param name="id">Id of the Refresh Token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call consumed the token, false if it was already consumed.</returns>
    public Task<bool> TryConsumeRefreshToken(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes a single Access Token. Does nothing if it is already revoked.
    /// </summary>
    /// <param name="id">Id of the Access Token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the token is revoked.</returns>
    public Task RevokeAccessToken(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes a Grant, which makes all of its Access and Refresh Tokens invalid. Does nothing if it is already revoked.
    /// </summary>
    /// <param name="id">Id of the Grant.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Grant is revoked.</returns>
    public Task RevokeGrant(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes the Grant that was created from an Authorization Code, e.g. because the code was used twice.
    /// </summary>
    /// <param name="authorizationCodeId">Id of the Authorization Code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of Grants that were revoked by this call.</returns>
    public Task<int> RevokeGrantsOfAuthorizationCode(Guid authorizationCodeId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes all Grants of a Client, e.g. because the Client was disabled.
    /// </summary>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of Grants that were revoked by this call.</returns>
    public Task<int> RevokeGrantsOfClient(Guid clientId, DateTime now, CancellationToken cancellationToken = default);
}
