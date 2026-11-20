// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Consents.</param>
public sealed class ConsentRepository(AuthoraContext context) : IConsentRepository
{
    /// <inheritdoc/>
    public async Task<ITransaction> BeginConsentChangeAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        // A transaction alone is not enough: two first approvals could both find no Consent and insert one, and the
        // second would fail on the unique index. The lock makes the second wait, so it finds the Consent of the first.
        var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await context.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.Consents}, {LockKey(userId, clientId)})",
                cancellationToken);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }

        return transaction is null ? JoinedTransaction.Instance : new EfTransaction(transaction);
    }

    /// <inheritdoc/>
    public async Task<Consent?> GetConsent(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        return await context.Consents
            .Include(consent => consent.Scopes)
            .FirstOrDefaultAsync(consent => consent.UserId == userId && consent.ClientId == clientId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Consent>> GetConsentsOfUser(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Consents
            .AsNoTracking()
            .AsSplitQuery()
            .Include(consent => consent.Client)
            .Include(consent => consent.Scopes)
            .Where(consent => consent.UserId == userId)
            .OrderBy(consent => consent.Client!.Name)
            .ThenBy(consent => consent.ClientId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public void AddConsent(Consent consent)
    {
        context.Consents.Add(consent);
    }

    /// <inheritdoc/>
    public void RemoveConsent(Consent consent)
    {
        context.Consents.Remove(consent);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteConsentsOfUser(Guid userId, CancellationToken cancellationToken = default)
    {
        // The Scopes of the Consents are deleted by the database (ON DELETE CASCADE).
        return await context.Consents
            .Where(consent => consent.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     The second key of the lock, the same in every instance (unlike <see cref="HashCode"/>). Two Consents sharing
    ///     a key only wait for each other briefly.
    /// </summary>
    private static int LockKey(Guid userId, Guid clientId)
    {
        Span<byte> bytes = stackalloc byte[32];
        userId.TryWriteBytes(bytes);
        clientId.TryWriteBytes(bytes[16..]);

        var key = 17;

        for (var offset = 0; offset < bytes.Length; offset += sizeof(int))
        {
            key = (key * 31) ^ BitConverter.ToInt32(bytes[offset..]);
        }

        return key;
    }

    /// <summary>
    ///     Stands for the transaction of the caller that the lock joined; its owner commits it.
    /// </summary>
    private sealed class JoinedTransaction : ITransaction
    {
        public static readonly JoinedTransaction Instance = new();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
