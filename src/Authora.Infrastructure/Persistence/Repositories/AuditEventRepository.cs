// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to read the events.</param>
public sealed class AuditEventRepository(AuthoraContext context) : IAuditEventRepository
{
    /// <inheritdoc/>
    public async Task<PagedResponse<AuditEventResponse>> Search(
        AuditEventFilter filter,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(page);

        var query = context.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            var type = filter.Type.Trim();
            var area = $"{type}.";

            // An area matches all of its Types, e.g. "user" matches "user.created".
            query = query.Where(auditEvent => auditEvent.Type == type || auditEvent.Type.StartsWith(area));
        }

        if (filter.ActorUserId is not null)
        {
            query = query.Where(auditEvent => auditEvent.ActorUserId == filter.ActorUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetType))
        {
            query = query.Where(auditEvent => auditEvent.TargetType == filter.TargetType);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetId))
        {
            query = query.Where(auditEvent => auditEvent.TargetId == filter.TargetId);
        }

        if (filter.Outcome is not null)
        {
            query = query.Where(auditEvent => auditEvent.Outcome == filter.Outcome);
        }

        if (filter.From is not null)
        {
            var from = filter.From.Value.UtcDateTime;
            query = query.Where(auditEvent => auditEvent.OccurredAt >= from);
        }

        if (filter.To is not null)
        {
            var to = filter.To.Value.UtcDateTime;
            query = query.Where(auditEvent => auditEvent.OccurredAt < to);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await Project(query
                .OrderByDescending(auditEvent => auditEvent.OccurredAt)
                .ThenByDescending(auditEvent => auditEvent.Id)
                .Skip(page.Skip)
                .Take(page.Size))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AuditEventResponse>(items, total);
    }

    /// <inheritdoc/>
    public async Task<AuditEventResponse?> GetLatest(string type, CancellationToken cancellationToken = default)
    {
        return await Project(context.AuditEvents
                .AsNoTracking()
                .Where(auditEvent => auditEvent.Type == type)
                .OrderByDescending(auditEvent => auditEvent.OccurredAt)
                .ThenByDescending(auditEvent => auditEvent.Id)
                .Take(1))
            .FirstOrDefaultAsync(cancellationToken);
    }

    // The Email and Client Name are looked up when reading, so they are current and are not copied into the log.
    private IQueryable<AuditEventResponse> Project(IQueryable<AuditEvent> events)
    {
        return events.Select(auditEvent => new AuditEventResponse(
            auditEvent.Id,
            auditEvent.OccurredAt,
            auditEvent.Type,
            auditEvent.Outcome,
            auditEvent.ActorUserId,
            context.Users.Where(user => user.Id == auditEvent.ActorUserId).Select(user => user.Email).FirstOrDefault(),
            auditEvent.ActorClientId,
            context.Clients.Where(client => client.Id == auditEvent.ActorClientId).Select(client => client.Name).FirstOrDefault(),
            auditEvent.IpAddress,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.Details));
    }
}
