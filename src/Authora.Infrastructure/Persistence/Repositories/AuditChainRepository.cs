// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to read the chain.</param>
public sealed class AuditChainRepository(AuthoraContext context) : IAuditChainRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<(AuditChainLink Link, AuditEvent? Event)>> GetLinks(
        long afterSequence,
        int count,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.AuditChainLinks
            .AsNoTracking()
            .Where(link => link.Sequence > afterSequence)
            .OrderBy(link => link.Sequence)
            .Take(count)
            .Select(link => new
            {
                Link = link,
                Event = context.AuditEvents.FirstOrDefault(auditEvent => auditEvent.Id == link.EventId)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.Link, row.Event)).ToList();
    }

    /// <inheritdoc/>
    public async Task<AuditChainLink?> GetLink(long sequence, CancellationToken cancellationToken = default)
    {
        return await context.AuditChainLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(link => link.Sequence == sequence, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountUnsealedEvents(DateTime occurredBefore, CancellationToken cancellationToken = default)
    {
        return await context.AuditEvents
            .Where(auditEvent => auditEvent.OccurredAt < occurredBefore)
            .CountAsync(auditEvent => !context.AuditChainLinks.Any(link => link.EventId == auditEvent.Id), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsTruncationRecorded(long firstSequence, CancellationToken cancellationToken = default)
    {
        var targetId = firstSequence.ToString(CultureInfo.InvariantCulture);

        return await context.AuditEvents.AnyAsync(
            auditEvent => auditEvent.Type == AuditEventTypes.System.AuditChainTruncated
                && auditEvent.TargetType == AuditTargetTypes.AuditChainLink
                && auditEvent.TargetId == targetId,
            cancellationToken);
    }
}
