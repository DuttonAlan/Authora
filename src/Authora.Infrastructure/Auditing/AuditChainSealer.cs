// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit.Chain;
using Authora.Core.Domain.Entities;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Auditing;

/// <summary>
///     Seals new audit events into the hash chain, oldest first.
/// </summary>
/// <remarks>
///     <para>
///         Sealing runs after the events were committed instead of while they are saved, so audited changes are not
///         serialized behind one lock, and <c>audit.events</c> needs no <c>UPDATE</c>: the links are inserted into
///         <c>audit.chain</c>. Until it runs, a new event can be changed without a trace.
///     </para>
///     <para>
///         Only events since the newest sealed event minus <see cref="AuditChainOptions.LateEventWindow"/> are
///         considered, so the lookup stays small. The first run seals all existing events. Safe to run on several
///         instances at the same time: they take turns under a lock, which the cleanup of old events takes too.
///     </para>
///     <para>
///         Every batch is logged as a checkpoint with its last Sequence and hash. Shipped to a separate system, the
///         checkpoints detect a chain that was rebuilt after a change.
///     </para>
/// </remarks>
/// <param name="context">The DbContext to read the events and add the links.</param>
/// <param name="options">The batch size.</param>
/// <param name="timeProvider">The clock for the time of sealing.</param>
/// <param name="logger">The Logger to write the checkpoints.</param>
public sealed class AuditChainSealer(
    AuthoraContext context,
    IOptions<AuditChainOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditChainSealer> logger)
{
    /// <summary>
    ///     Seals all events that are not sealed yet.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the sealing.</param>
    /// <returns>The number of sealed events.</returns>
    public async Task<int> SealAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = options.Value.BatchSize;
        var total = 0;
        int sealedEvents;

        do
        {
            sealedEvents = await SealBatchAsync(batchSize, cancellationToken);
            total += sealedEvents;
        }
        while (sealedEvents == batchSize);

        return total;
    }

    private async Task<int> SealBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Instances take turns, so the next one continues the chain where the previous one ended.
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.AuditChain})", cancellationToken);

        var last = await context.AuditChainLinks
            .AsNoTracking()
            .OrderByDescending(link => link.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

        var unsealed = context.AuditEvents
            .AsNoTracking()
            .Where(auditEvent => !context.AuditChainLinks.Any(link => link.EventId == auditEvent.Id));

        if (last is not null)
        {
            // Counted back from the newest sealed event, not from the time of sealing: a backlog larger than one
            // batch, e.g. on the first run or after an outage, otherwise falls out of the window after the first batch.
            var lastOccurredAt = await context.AuditEvents
                .AsNoTracking()
                .Where(auditEvent => auditEvent.Id == last.EventId)
                .Select(auditEvent => (DateTime?)auditEvent.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            var newest = lastOccurredAt is { } occurredAt && occurredAt < last.SealedAt ? occurredAt : last.SealedAt;
            var since = newest - AuditChainOptions.LateEventWindow;
            unsealed = unsealed.Where(auditEvent => auditEvent.OccurredAt >= since);
        }

        // Read back from the database, so the hash covers exactly what is stored, e.g. microseconds.
        var events = await unsealed
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ThenBy(auditEvent => auditEvent.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var sequence = last?.Sequence ?? 0;
        var previousHash = last?.Hash ?? AuditChainHash.Genesis;

        foreach (var auditEvent in events)
        {
            sequence++;
            var hash = AuditChainHash.Compute(sequence, previousHash, auditEvent);

            context.AuditChainLinks.Add(AuditChainLink.Create(sequence, auditEvent.Id, previousHash, hash, now));
            previousHash = hash;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        context.ChangeTracker.Clear();

        logger.AuditEventsSealed(events.Count, sequence, previousHash);

        return events.Count;
    }
}
