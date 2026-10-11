// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

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
    public async Task<Session?> GetSessionByTokenHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.Sessions
            .FirstOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
