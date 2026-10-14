// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence;

/// <inheritdoc/>
/// <param name="context">The DbContext of the request, which the repositories save.</param>
/// <param name="options">The options to create a DbContext of its own for <see cref="RecordNowAsync"/>.</param>
/// <param name="actorAccessor">Provides the actor of the current request.</param>
/// <param name="timeProvider">The clock for the time of the event.</param>
public sealed class AuditLog(
    AuthoraContext context,
    DbContextOptions<AuthoraContext> options,
    IAuditActorAccessor actorAccessor,
    TimeProvider timeProvider) : IAuditLog
{
    /// <inheritdoc/>
    public void Record(AuditEntry entry)
    {
        context.AuditEvents.Add(Create(entry));
    }

    /// <inheritdoc/>
    public async Task RecordNowAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        // A DbContext of its own, so unsaved changes of the request are not saved along with the event.
        await using var ownContext = new AuthoraContext(options);

        ownContext.AuditEvents.Add(Create(entry));
        await ownContext.SaveChangesAsync(cancellationToken);
    }

    private AuditEvent Create(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var actor = actorAccessor.Current;

        return AuditEvent.Create(
            entry.Type,
            entry.Outcome,
            timeProvider.GetUtcNow().UtcDateTime,
            actor.UserId,
            entry.ClientId ?? actor.ClientId,
            actor.IpAddress,
            entry.TargetType,
            entry.TargetId,
            entry.Details);
    }
}
