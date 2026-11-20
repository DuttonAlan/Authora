// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the email tokens.</param>
public sealed class EmailTokenRepository(AuthoraContext context) : IEmailTokenRepository
{
    /// <inheritdoc/>
    public async Task CreateEmailToken(EmailToken emailToken, CancellationToken cancellationToken = default)
    {
        context.EmailTokens.Add(emailToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<EmailToken?> GetEmailTokenByHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.EmailTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "ConsumedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.EmailTokens
            .Where(token => token.Id == id && token.ConsumedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, now), cancellationToken);

        return updated == 1;
    }

    /// <inheritdoc/>
    public async Task<int> RevokeEmailTokens(Guid userId, EmailTokenPurpose purpose, DateTime now, CancellationToken cancellationToken = default)
    {
        return await context.EmailTokens
            .Where(token => token.UserId == userId && token.Purpose == purpose && token.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ConsumedAt, now), cancellationToken);
    }
}
