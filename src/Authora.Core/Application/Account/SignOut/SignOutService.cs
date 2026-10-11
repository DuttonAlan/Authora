// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account.SignOut;

/// <summary>
///     The Service to end the current Session on the server.
/// </summary>
/// <param name="sessionRepository">An Instance of a ISessionRepository to revoke the Session.</param>
/// <param name="secretHasher">The hasher to look up the Session token.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="logger">The Logger to record the sign-out.</param>
public sealed class SignOutService(
    ISessionRepository sessionRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<SignOutService> logger)
{
    /// <summary>
    ///     Revokes the Session, so its token can never be used again. Does nothing if the Session does not exist.
    /// </summary>
    /// <param name="sessionToken">The plain-text Session token from the cookie, or null.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is revoked.</returns>
    public async Task ExecuteAsync(string? sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return;
        }

        var session = await sessionRepository.GetSessionByTokenHash(secretHasher.Hash(sessionToken), cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await sessionRepository.SaveChangesAsync(cancellationToken);

        logger.SignedOut(session.UserId, session.Id);
    }
}
