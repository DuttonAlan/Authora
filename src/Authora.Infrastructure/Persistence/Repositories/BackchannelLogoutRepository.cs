// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to write the notifications.</param>
public sealed class BackchannelLogoutRepository(AuthoraContext context) : IBackchannelLogoutRepository
{
    /// <inheritdoc/>
    public async Task<int> EnqueueForSessions(IReadOnlyCollection<Guid> sessionIds, DateTime now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);

        if (sessionIds.Count == 0)
        {
            return 0;
        }

        // A single INSERT ... SELECT, so the Clients are found and the rows written in one statement.
        return await context.Database.ExecuteSqlRawAsync(
            BackchannelLogoutNotificationConfiguration.EnqueueSql,
            [sessionIds.ToArray(), now],
            cancellationToken);
    }
}
