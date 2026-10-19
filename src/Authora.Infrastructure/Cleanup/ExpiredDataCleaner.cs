// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Deletes Authorization Codes, Grants, Access and Refresh Tokens and Sessions that expired or were revoked more
///     than <see cref="CleanupOptions.Retention"/> ago, and audit events older than
///     <see cref="CleanupOptions.AuditRetention"/> with their links of the hash chain.
/// </summary>
/// <remarks>
///     Rows are deleted in batches of <see cref="CleanupOptions.BatchSize"/>, each in its own statement, so a large
///     backlog does not lock a table for long. The audit events are deleted in one transaction under the lock of
///     <see cref="Auditing.AuditChainSealer"/>. Safe to run on several instances at the same time.
/// </remarks>
/// <param name="context">The DbContext to delete from.</param>
/// <param name="options">The retention and batch size.</param>
/// <param name="auditLog">The audit log to record where the hash chain starts after deleting its start.</param>
/// <param name="logger">The Logger to record how many rows were deleted.</param>
public sealed class ExpiredDataCleaner(
    AuthoraContext context,
    IOptions<CleanupOptions> options,
    IAuditLog auditLog,
    ILogger<ExpiredDataCleaner> logger)
{
    /// <summary>
    ///     Deletes everything that expired or was revoked before <paramref name="now"/> minus the retention.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the cleanup.</param>
    /// <returns>A Task that completes once all batches are deleted.</returns>
    public async Task CleanAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var cutoff = now - options.Value.Retention;

        // Consumed codes and Refresh Tokens are kept until the cutoff too, so reuse detection still works meanwhile.
        var authorizationCodes = await DeleteInBatchesAsync(
            context.AuthorizationCodes.Where(code => code.ExpiresAt < cutoff).Select(code => code.Id),
            ids => context.AuthorizationCodes.Where(code => ids.Contains(code.Id)),
            cancellationToken);

        var accessTokens = await DeleteInBatchesAsync(
            context.AccessTokens.Where(token => token.ExpiresAt < cutoff).Select(token => token.Id),
            ids => context.AccessTokens.Where(token => ids.Contains(token.Id)),
            cancellationToken);

        var refreshTokens = await DeleteInBatchesAsync(
            context.RefreshTokens.Where(token => token.ExpiresAt < cutoff).Select(token => token.Id),
            ids => context.RefreshTokens.Where(token => ids.Contains(token.Id)),
            cancellationToken);

        // The remaining tokens of a revoked Grant are invalid anyway. They are deleted here in batches, because the
        // cascade below would delete all tokens of a Grant in one statement, and a long-running Grant can have many.
        var revokedGrantIds = context.Grants.Where(grant => grant.RevokedAt < cutoff).Select(grant => grant.Id);

        accessTokens += await DeleteInBatchesAsync(
            context.AccessTokens.Where(token => revokedGrantIds.Contains(token.GrantId)).Select(token => token.Id),
            ids => context.AccessTokens.Where(token => ids.Contains(token.Id)),
            cancellationToken);

        refreshTokens += await DeleteInBatchesAsync(
            context.RefreshTokens.Where(token => revokedGrantIds.Contains(token.GrantId)).Select(token => token.Id),
            ids => context.RefreshTokens.Where(token => ids.Contains(token.Id)),
            cancellationToken);

        // After the tokens: a Grant without tokens can no longer be used, and a revoked Grant has none left.
        var grants = await DeleteInBatchesAsync(
            context.Grants
                .Where(grant => grant.RevokedAt < cutoff
                    || (grant.CreatedAt < cutoff
                        && !context.AccessTokens.Any(token => token.GrantId == grant.Id)
                        && !context.RefreshTokens.Any(token => token.GrantId == grant.Id)))
                .Select(grant => grant.Id),
            ids => context.Grants.Where(grant => ids.Contains(grant.Id)),
            cancellationToken);

        var sessions = await DeleteInBatchesAsync(
            context.Sessions.Where(session => session.ExpiresAt < cutoff || session.RevokedAt < cutoff).Select(session => session.Id),
            ids => context.Sessions.Where(session => ids.Contains(session.Id)),
            cancellationToken);

        logger.ExpiredDataDeleted(authorizationCodes, accessTokens, refreshTokens, grants, sessions, cutoff);

        var auditCutoff = now - options.Value.AuditRetention;
        var auditEvents = await DeleteAuditEventsAsync(auditCutoff, cancellationToken);

        logger.AuditEventsDeleted(auditEvents, auditCutoff);
    }

    /// <summary>
    ///     Deletes the audit events that occurred before the cutoff, and their links of the hash chain.
    /// </summary>
    /// <remarks>
    ///     The chain is only cut from its start, up to the first link with an event that is kept, so the remaining
    ///     links still verify. An expired event sealed after a newer one is therefore kept until that one expires.
    ///     The last link is always kept, so the chain continues instead of starting again.
    /// </remarks>
    private async Task<int> DeleteAuditEventsAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        // Under the lock of the sealing, so it cannot link an event that is deleted here, or add links while the
        // first kept link is determined. Sealing waits meanwhile.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.AuditChain})", cancellationToken);

        // Events that were never sealed, e.g. older than the first sealing looked back. Deleted before the cut is
        // recorded below, so that record is never one of them.
        var auditEvents = await DeleteInBatchesAsync(
            context.AuditEvents
                .Where(auditEvent => auditEvent.OccurredAt < cutoff)
                .Where(auditEvent => !context.AuditChainLinks.Any(link => link.EventId == auditEvent.Id))
                .Select(auditEvent => auditEvent.Id),
            ids => context.AuditEvents.Where(auditEvent => ids.Contains(auditEvent.Id)),
            cancellationToken);

        var firstKept = await context.AuditChainLinks
            .Where(link => context.AuditEvents.Any(auditEvent => auditEvent.Id == link.EventId && auditEvent.OccurredAt >= cutoff))
            .OrderBy(link => link.Sequence)
            .Select(link => (long?)link.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await context.AuditChainLinks.MaxAsync(link => (long?)link.Sequence, cancellationToken)
            ?? 0;

        var expiredLinks = context.AuditChainLinks.Where(link => link.Sequence < firstKept);

        // Recorded before deleting and sealed into the chain like every event, so the verification can tell this cut
        // from links deleted directly in the database. Recorded again if a cleanup stopped halfway.
        if (await expiredLinks.AnyAsync(cancellationToken))
        {
            await auditLog.RecordNowAsync(
                new AuditEntry(
                    AuditEventTypes.System.AuditChainTruncated,
                    AuditTargetTypes.AuditChainLink,
                    firstKept.ToString(CultureInfo.InvariantCulture),
                    $"Links before Sequence {firstKept} with events before {cutoff:O} deleted after the retention"),
                cancellationToken);

            logger.AuditChainTruncated(firstKept, cutoff);
        }

        // The events first: a link without its event would be reported as a deleted event if the cleanup stops here.
        auditEvents += await DeleteInBatchesAsync(
            context.AuditEvents
                .Where(auditEvent => expiredLinks.Any(link => link.EventId == auditEvent.Id))
                .Select(auditEvent => auditEvent.Id),
            ids => context.AuditEvents.Where(auditEvent => ids.Contains(auditEvent.Id)),
            cancellationToken);

        await DeleteInBatchesAsync(
            expiredLinks.Select(link => link.Sequence),
            sequences => context.AuditChainLinks.Where(link => sequences.Contains(link.Sequence)),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return auditEvents;
    }

    private Task<int> DeleteInBatchesAsync<TEntity>(
        IQueryable<Guid> expiredIds,
        Func<IQueryable<Guid>, IQueryable<TEntity>> rowsWithIds,
        CancellationToken cancellationToken)
    {
        return DeleteInBatchesAsync<TEntity, Guid>(expiredIds, rowsWithIds, cancellationToken);
    }

    private async Task<int> DeleteInBatchesAsync<TEntity, TKey>(
        IQueryable<TKey> expiredIds,
        Func<IQueryable<TKey>, IQueryable<TEntity>> rowsWithIds,
        CancellationToken cancellationToken)
    {
        var batchSize = options.Value.BatchSize;
        var total = 0;
        int deleted;

        do
        {
            // DELETE ... WHERE "Id" IN (SELECT "Id" ... LIMIT @batchSize)
            deleted = await rowsWithIds(expiredIds.Take(batchSize)).ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == batchSize);

        return total;
    }
}
