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
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            logger.EmailTokenUnknown(purpose);

            return null;
        }

        var emailToken = await emailTokenRepository.GetEmailTokenByHash(secretHasher.Hash(token), cancellationToken);

        if (emailToken is null)
        {
            logger.EmailTokenUnknown(purpose);

            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var reason = emailToken switch
        {
            _ when emailToken.Purpose != purpose => $"issued for {emailToken.Purpose}",
            _ when emailToken.ConsumedAt is not null => "already used or replaced",
            _ when emailToken.IsExpired(now) => "expired",
            _ => null
        };

        if (reason is not null)
        {
            logger.EmailTokenRejected(emailToken.Id, emailToken.UserId, purpose, reason);

            return null;
        }

        var user = await userRepository.GetUserById(emailToken.UserId, cancellationToken);

        if (user is null || user.State == UserState.Deleted || !emailToken.IsFor(user))
        {
            logger.EmailTokenRejected(emailToken.Id, emailToken.UserId, purpose, "the User no longer has the address it was sent to");

            return null;
        }

        // Atomic, so of concurrent requests with the same token only one gets the User.
        if (!await emailTokenRepository.TryConsume(emailToken.Id, now, cancellationToken))
        {
            logger.EmailTokenRejected(emailToken.Id, emailToken.UserId, purpose, "already used or replaced");

            return null;
        }

        logger.EmailTokenUsed(emailToken.Id, purpose, user.Id);

        return user;
    }
}
