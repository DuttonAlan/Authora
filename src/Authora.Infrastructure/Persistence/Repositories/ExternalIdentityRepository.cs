// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the external identities.</param>
public sealed class ExternalIdentityRepository(AuthoraContext context) : IExternalIdentityRepository
{
    /// <inheritdoc/>
    public async Task<ExternalIdentity?> FindIdentity(Guid providerId, string subject, CancellationToken cancellationToken = default)
    {
        return await context.ExternalIdentities
            .FirstOrDefaultAsync(identity => identity.ProviderId == providerId && identity.Subject == subject, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExternalIdentity>> GetIdentitiesOfUser(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ExternalIdentities
            .Where(identity => identity.UserId == userId)
            .OrderBy(identity => identity.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryCreateIdentity(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        context.ExternalIdentities.Add(identity);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Nothing of this save is kept, including the audit event staged for it, so a later save of the request
            // does not try again.
            foreach (var entry in context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void RemoveIdentities(IEnumerable<ExternalIdentity> identities)
    {
        context.ExternalIdentities.RemoveRange(identities);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
