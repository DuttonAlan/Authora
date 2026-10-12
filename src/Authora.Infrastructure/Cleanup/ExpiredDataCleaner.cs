// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Deletes Authorization Codes, Grants, Access and Refresh Tokens and Sessions that expired or were revoked more
///     than <see cref="CleanupOptions.Retention"/> ago.
/// </summary>
/// <remarks>
///     Rows are deleted in batches of <see cref="CleanupOptions.BatchSize"/>, each in its own statement, so a large
///     backlog does not lock a table for long. Safe to run on several instances at the same time.
/// </remarks>
/// <param name="context">The DbContext to delete from.</param>
/// <param name="options">The retention and batch size.</param>
/// <param name="logger">The Logger to record how many rows were deleted.</param>
public sealed class ExpiredDataCleaner(
    AuthoraContext context,
    IOptions<CleanupOptions> options,
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

        // After the tokens: a Grant without tokens can no longer be used. Deleting a revoked Grant also deletes its
        // remaining tokens (cascade), which are invalid anyway.
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
    }

    private async Task<int> DeleteInBatchesAsync<TEntity>(
        IQueryable<Guid> expiredIds,
        Func<IQueryable<Guid>, IQueryable<TEntity>> rowsWithIds,
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
