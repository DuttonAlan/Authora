// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account.SignOut;

/// <summary>
///     The Service to end the current Session on the server.
/// </summary>
/// <remarks>
///     The Grants approved in the Session are revoked with it, and Clients with a Back-Channel Logout URI that received
///     tokens in it are notified, see <see cref="SessionRevoker"/>.
/// </remarks>
/// <param name="sessionRepository">An Instance of a ISessionRepository to find the Session.</param>
/// <param name="sessionRevoker">Revokes the Session and its Grants and notifies its Clients.</param>
/// <param name="secretHasher">The hasher to look up the Session token.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the sign-out.</param>
/// <param name="logger">The Logger to record the sign-out.</param>
public sealed class SignOutService(
    ISessionRepository sessionRepository,
    SessionRevoker sessionRevoker,
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

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await sessionRevoker.BeginAsync(cancellationToken);

        // Revoked by its Id, so the User also signs out if a step-up gave the Session a new token meanwhile. Of
        // concurrent sign-outs, only one revokes it and records the event.
        if (!await sessionRevoker.RevokeSessionAsync(session.UserId, session.Id, now, cancellationToken))
        {
            return;
        }

        var sessionDetails = details is null ? $"Session {session.Id}" : $"Session {session.Id}, {details}";
        auditLog.Record(AuditEntry.ForUser(
            client is null ? AuditEventTypes.Account.SignedOut : AuditEventTypes.Account.SignedOutByClient,
            session.UserId,
            sessionDetails));

        // Only the audit event is new: the loaded Session itself is not changed, so this cannot conflict with a step-up.
        await sessionRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
