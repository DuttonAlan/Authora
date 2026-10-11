// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Grants and tokens.</param>
public sealed class TokenRepository(AuthoraContext context) : ITokenRepository
{
    /// <inheritdoc/>
    public async Task CreateGrant(Grant grant, AccessToken accessToken, RefreshToken? refreshToken, CancellationToken cancellationToken = default)
    {
        context.Grants.Add(grant);
        await AddTokens(accessToken, refreshToken, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddTokens(AccessToken accessToken, RefreshToken? refreshToken, CancellationToken cancellationToken = default)
    {
        context.AccessTokens.Add(accessToken);

        if (refreshToken is not null)
        {
            context.RefreshTokens.Add(refreshToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AccessToken?> GetAccessTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.AccessTokens
            .AsNoTracking()
            .Include(token => token.Grant)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<RefreshToken?> GetRefreshTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.RefreshTokens
            .AsNoTracking()
            .Include(token => token.Grant)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsumeRefreshToken(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "ConsumedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.RefreshTokens
            .Where(token => token.Id == id && token.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, now), cancellationToken);

        return updated == 1;
    }

    /// <inheritdoc/>
    public async Task RevokeAccessToken(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        await context.AccessTokens
            .Where(token => token.Id == id && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RevokeGrant(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        await RevokeGrants(context.Grants.Where(grant => grant.Id == id), now, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> RevokeGrantsOfAuthorizationCode(Guid authorizationCodeId, DateTime now, CancellationToken cancellationToken = default)
    {
        return await RevokeGrants(context.Grants.Where(grant => grant.AuthorizationCodeId == authorizationCodeId), now, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> RevokeGrantsOfClient(Guid clientId, DateTime now, CancellationToken cancellationToken = default)
    {
        return await RevokeGrants(context.Grants.Where(grant => grant.ClientId == clientId), now, cancellationToken);
    }

    private static async Task<int> RevokeGrants(IQueryable<Grant> grants, DateTime now, CancellationToken cancellationToken)
    {
        return await grants
            .Where(grant => grant.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(grant => grant.RevokedAt, now), cancellationToken);
    }
}
