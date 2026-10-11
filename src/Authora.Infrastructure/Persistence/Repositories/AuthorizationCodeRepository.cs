// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Authorization Codes.</param>
public sealed class AuthorizationCodeRepository(AuthoraContext context) : IAuthorizationCodeRepository
{
    /// <inheritdoc/>
    public async Task CreateAuthorizationCode(AuthorizationCode authorizationCode, CancellationToken cancellationToken = default)
    {
        context.AuthorizationCodes.Add(authorizationCode);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AuthorizationCode?> GetAuthorizationCodeByHash(SecretHash codeHash, CancellationToken cancellationToken = default)
    {
        return await context.AuthorizationCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(code => code.CodeHash == codeHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "ConsumedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.AuthorizationCodes
            .Where(code => code.Id == id && code.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.ConsumedAt, now), cancellationToken);

        return updated == 1;
    }

    /// <inheritdoc/>
    public async Task MarkReused(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        await context.AuthorizationCodes
            .Where(code => code.Id == id && code.ReusedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.ReusedAt, now), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsReused(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.AuthorizationCodes
            .AnyAsync(code => code.Id == id && code.ReusedAt != null, cancellationToken);
    }
}
