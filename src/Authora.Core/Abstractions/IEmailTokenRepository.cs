// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for operations on the one-time tokens of email links.
/// </summary>
public interface IEmailTokenRepository
{
    /// <summary>
    ///     Saves a new email token and marks the unused tokens of its User for its purpose as consumed, so only the
    ///     new one can be used.
    /// </summary>
    /// <remarks>
    ///     Atomic and serialized per User and purpose: of concurrent calls, the token saved last is the only usable one.
    /// </remarks>
    /// <param name="emailToken">The email token to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the email token is saved.</returns>
    public Task ReplaceEmailTokens(EmailToken emailToken, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get an email token by the hash of its token. The email token is not tracked.
    /// </summary>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The email token, or null if no email token has this token.</returns>
    public Task<EmailToken?> GetEmailTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks an email token as consumed in a single atomic update, unless it was already consumed or has expired.
    /// </summary>
    /// <remarks>
    ///     Of several concurrent requests with the same token, exactly one succeeds.
    /// </remarks>
    /// <param name="id">Id of the email token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call consumed the token, false if it was already consumed or has expired.</returns>
    public Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks all unused email tokens of a User for a purpose as consumed, e.g. before a new one is sent.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="purpose">The purpose of the tokens.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of tokens that were still unused.</returns>
    public Task<int> RevokeEmailTokens(Guid userId, EmailTokenPurpose purpose, DateTime now, CancellationToken cancellationToken = default);
}
