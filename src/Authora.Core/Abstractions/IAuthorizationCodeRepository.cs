// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Authorization Code operations.
/// </summary>
public interface IAuthorizationCodeRepository
{
    /// <summary>
    ///     Create a new Authorization Code.
    /// </summary>
    /// <param name="authorizationCode">The Authorization Code to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Authorization Code is saved.</returns>
    public Task CreateAuthorizationCode(AuthorizationCode authorizationCode, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get an Authorization Code by the hash of its code. The Authorization Code is not tracked.
    /// </summary>
    /// <param name="codeHash">The hash of the code.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Authorization Code, or null if no Authorization Code has this code.</returns>
    public Task<AuthorizationCode?> GetAuthorizationCodeByHash(SecretHash codeHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks an Authorization Code as consumed in a single atomic update, unless it was already consumed.
    /// </summary>
    /// <remarks>
    ///     Of several concurrent requests with the same code, exactly one succeeds.
    /// </remarks>
    /// <param name="id">Id of the Authorization Code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call consumed the code, false if it was already consumed.</returns>
    public Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default);
}
