// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;
using Authora.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Sessions.</param>
public sealed class SessionRepository(AuthoraContext context) : ISessionRepository
{
    /// <inheritdoc/>
    public async Task CreateSession(Session session, CancellationToken cancellationToken = default)
    {
        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Session?> GetSessionById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Sessions
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Session?> GetSessionByTokenHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.Sessions
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeSessionIfSecurityStampChanged(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE compares the stamps of the Session and the User as they are stored now, so a Session that a
        // password change renewed in the meantime is not revoked.
        var revoked = await context.Sessions
            .Where(session => session.Id == id
                && session.RevokedAt == null
                && context.Users.Any(user => user.Id == session.UserId && user.SecurityStamp != session.SecurityStamp))
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);

        return revoked > 0;
    }

    /// <inheritdoc/>
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            return JoinedTransaction.Instance;
        }

        return new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));
    }

    /// <inheritdoc/>
    public async Task LockSession(Guid id, CancellationToken cancellationToken = default)
    {
        // Outside a transaction, the lock would end with the statement.
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A Session can only be locked in a transaction.");
        }

        await context.Database.ExecuteSqlRawAsync(SessionConfiguration.LockSql, [id], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeSession(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        var revoked = await context.Database
            .SqlQueryRaw<Guid>(SessionConfiguration.RevokeSql, id, now)
            .ToListAsync(cancellationToken);

        return revoked.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> RevokeSessionsOfUser(
        Guid userId,
        Guid? exceptSessionId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(SessionConfiguration.RevokeOfUserSql, userId, now, exceptSessionId ?? Guid.Empty)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReloadSession(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await context.Entry(session).ReloadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Detach the outdated Sessions, so loading them again returns the current values from the database.
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new ConflictException("The Session was changed by another request, e.g. it got a new token.", exception);
        }
    }
}
