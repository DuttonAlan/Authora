// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account.SignOut;

/// <summary>
///     The Service to end the current Session on the server.
/// </summary>
/// <param name="sessionRepository">An Instance of a ISessionRepository to revoke the Session.</param>
/// <param name="secretHasher">The hasher to look up the Session token.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the sign-out.</param>
/// <param name="logger">The Logger to record the sign-out.</param>
public sealed class SignOutService(
    ISessionRepository sessionRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<SignOutService> logger)
{
    /// <summary>
    ///     Revokes the Session, so its token can never be used again. Does nothing if the Session does not exist.
    /// </summary>
    /// <param name="sessionToken">The plain-text Session token from the cookie, or null.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is revoked.</returns>
    public Task ExecuteAsync(string? sessionToken, CancellationToken cancellationToken = default)
    {
        return SignOutAsync(sessionToken, client: null, details: null, cancellationToken);
    }

    /// <summary>
    ///     Revokes the Session at the request of a Client (RP-initiated logout). Does nothing if the Session does not
    ///     exist. Recorded as <see cref="AuditEventTypes.Account.SignedOutByClient"/> if the Client is known.
    /// </summary>
    /// <param name="sessionToken">The plain-text Session token from the cookie, or null.</param>
    /// <param name="client">The Client that asked, or null if the request named none.</param>
    /// <param name="confirmedByUser">
    ///     True if the User confirmed the sign-out, false if an <c>id_token_hint</c> of the Session was sent.
    /// </param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Session is revoked.</returns>
    public Task ExecuteForClientAsync(
        string? sessionToken,
        Client? client,
        bool confirmedByUser,
        CancellationToken cancellationToken = default)
    {
        var how = confirmedByUser ? "confirmed by the User" : "id_token_hint of the Session";
        var details = client is null
            ? $"logout request without Client, {how}"
            : $"Client {client.Id} ('{client.Name}'), {how}";

        return SignOutAsync(sessionToken, client, details, cancellationToken);
    }

    private async Task SignOutAsync(string? sessionToken, Client? client, string? details, CancellationToken cancellationToken)
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

        var sessionDetails = details is null ? $"Session {session.Id}" : $"Session {session.Id}, {details}";
        auditLog.Record(AuditEntry.ForUser(
            client is null ? AuditEventTypes.Account.SignedOut : AuditEventTypes.Account.SignedOutByClient,
            session.UserId,
            sessionDetails));

        await sessionRepository.SaveChangesAsync(cancellationToken);

        if (client is null)
        {
            logger.SignedOut(session.UserId, session.Id);
        }
        else
        {
            logger.SignedOutByClient(session.UserId, session.Id, client.Id);
        }
    }
}
