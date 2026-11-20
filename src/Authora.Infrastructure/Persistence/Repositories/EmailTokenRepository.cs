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
    public async Task ReplaceEmailTokens(EmailToken emailToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emailToken);

        // A transaction alone is not enough: two calls could both revoke before either inserts, leaving two usable
        // tokens. The lock makes the second call wait, so it revokes the token of the first. Joins the transaction
        // of the caller if there is one; the lock is then held until that transaction ends.
        var ownsTransaction = context.Database.CurrentTransaction is null;

        await using var transaction = ownsTransaction ? await context.Database.BeginTransactionAsync(cancellationToken) : null;

        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.EmailTokens}, {LockKey(emailToken.UserId, emailToken.Purpose)})",
            cancellationToken);

        await RevokeEmailTokens(emailToken.UserId, emailToken.Purpose, emailToken.CreatedAt, cancellationToken);

        context.EmailTokens.Add(emailToken);
        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
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

    /// <summary>
    ///     The second key of the lock. Two Users sharing a key only wait for each other briefly.
    /// </summary>
    private static int LockKey(Guid userId, EmailTokenPurpose purpose)
    {
        Span<byte> bytes = stackalloc byte[16];
        userId.TryWriteBytes(bytes);

        var key = (int)purpose;

        for (var offset = 0; offset < bytes.Length; offset += sizeof(int))
        {
            key = (key * 31) ^ BitConverter.ToInt32(bytes[offset..]);
        }

        return key;
    }
}
