// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Email;

/// <summary>
///     Issues the one-time tokens of email links. Only the newest token of a User per purpose can be used.
/// </summary>
/// <param name="emailTokenRepository">The repository to save the tokens.</param>
/// <param name="secretGenerator">Generates the random tokens.</param>
/// <param name="secretHasher">Hashes the tokens before they are stored.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">The Logger to record issued tokens.</param>
public sealed class EmailTokenIssuer(
    IEmailTokenRepository emailTokenRepository,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<EmailTokenIssuer> logger)
{
    /// <summary>
    ///     Issues a new token and invalidates the unused tokens the User already has for the purpose.
    /// </summary>
    /// <param name="user">The User the link is sent to, at their current address.</param>
    /// <param name="purpose">What the token may be used for.</param>
    /// <param name="lifetime">How long the token is valid, at most <see cref="EmailToken.MaxLifetime"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The plain-text token to put into the link. It is not stored and cannot be retrieved later.</returns>
    public async Task<string> IssueAsync(
        User user,
        EmailTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = secretGenerator.Generate();
        var emailToken = EmailToken.Create(secretHasher.Hash(token), user, purpose, now, lifetime);

        await emailTokenRepository.RevokeEmailTokens(user.Id, purpose, now, cancellationToken);
        await emailTokenRepository.CreateEmailToken(emailToken, cancellationToken);

        logger.EmailTokenIssued(emailToken.Id, purpose, user.Id, emailToken.ExpiresAt);

        return token;
    }
}
