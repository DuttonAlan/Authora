// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Email;

/// <summary>
///     Checks and uses up the one-time token of an email link.
/// </summary>
/// <param name="emailTokenRepository">The repository of the tokens.</param>
/// <param name="userRepository">The repository to load the User of a token.</param>
/// <param name="secretHasher">Hashes the presented token to find it.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record used and rejected tokens.</param>
public sealed class EmailTokenRedeemer(
    IEmailTokenRepository emailTokenRepository,
    IUserRepository userRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<EmailTokenRedeemer> logger)
{
    /// <summary>
    ///     Longest token that is looked up. Issued tokens are much shorter, so longer input is rejected right away.
    /// </summary>
    public const int MaxTokenLength = 128;

    /// <summary>
    ///     Uses up a token, so it cannot be used again.
    /// </summary>
    /// <remarks>
    ///     The token is accepted only if it exists, has the purpose, is unused and unexpired, and its User still exists
    ///     with the address it was sent to. Whether that User may do what the token is for, e.g. while deactivated, is
    ///     up to the caller.
    /// </remarks>
    /// <param name="token">The plain-text token from the link.</param>
    /// <param name="purpose">What the token is used for.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked User of the token, or null if the token is not accepted.</returns>
    public async Task<User?> RedeemAsync(string? token, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(token, purpose, cancellationToken) is not { } accepted)
        {
            return null;
        }

        var (stored, user) = accepted;

        var purposeName = purpose.LogName();

        // Atomic, so of concurrent requests with the same token only one gets the User.
        if (!await emailTokenRepository.TryConsume(stored.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken))
        {
            logger.EmailTokenRejected(stored.Id, stored.UserId, purposeName, "already used or replaced");

            return null;
        }

        logger.EmailTokenUsed(stored.Id, purposeName, user.Id);

        return user;
    }

    /// <summary>
    ///     Checks a token without using it up, e.g. to show a form only for a link that still works.
    /// </summary>
    /// <remarks>
    ///     Applies the same checks as <see cref="RedeemAsync"/>. A token that passes can still be used up by another
    ///     request before it is redeemed.
    /// </remarks>
    /// <param name="token">The plain-text token from the link.</param>
    /// <param name="purpose">What the token is used for.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked User of the token, or null if the token would not be accepted.</returns>
    public async Task<User?> CheckAsync(string? token, EmailTokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        return (await FindAsync(token, purpose, cancellationToken))?.User;
    }

    private async Task<(EmailToken Token, User User)?> FindAsync(
        string? token,
        EmailTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        var purposeName = purpose.LogName();

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            logger.EmailTokenUnknown(purposeName);

            return null;
        }

        var stored = await emailTokenRepository.GetEmailTokenByHash(secretHasher.Hash(token), cancellationToken);

        if (stored is null)
        {
            logger.EmailTokenUnknown(purposeName);

            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var reason = stored switch
        {
            _ when stored.Purpose != purpose => $"issued for {stored.Purpose.LogName()}",
            _ when stored.ConsumedAt is not null => "already used or replaced",
            _ when stored.IsExpired(now) => "expired",
            _ => null
        };

        if (reason is not null)
        {
            logger.EmailTokenRejected(stored.Id, stored.UserId, purposeName, reason);

            return null;
        }

        var user = await userRepository.GetUserById(stored.UserId, cancellationToken);

        if (user is null || user.State == UserState.Deleted || !stored.IsFor(user))
        {
            logger.EmailTokenRejected(stored.Id, stored.UserId, purposeName, "the User no longer has the address it was sent to");

            return null;
        }

        return (stored, user);
    }
}
