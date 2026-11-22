// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the authenticators and recovery codes.</param>
public sealed class MfaRepository(AuthoraContext context) : IMfaRepository
{
    /// <inheritdoc/>
    public async Task<ITransaction> BeginMfaChangeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Without the lock, e.g. a confirmation could save a secret that a restart replaced in the meantime, or new
        // recovery codes could be saved after a removal deleted the authenticator they stand in for.
        var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await context.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.Mfa}, {LockKey(userId)})",
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
    public async Task<TotpAuthenticator?> GetTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.TotpAuthenticators
            .FirstOrDefaultAsync(authenticator => authenticator.UserId == userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TotpAuthenticator?> FindConfirmedTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.TotpAuthenticators
            .AsNoTracking()
            .FirstOrDefaultAsync(authenticator => authenticator.UserId == userId && authenticator.ConfirmedAt != null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> HasConfirmedTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.TotpAuthenticators
            .AnyAsync(authenticator => authenticator.UserId == userId && authenticator.ConfirmedAt != null, cancellationToken);
    }

    /// <inheritdoc/>
    public void AddTotpAuthenticator(TotpAuthenticator authenticator)
    {
        context.TotpAuthenticators.Add(authenticator);
    }

    /// <inheritdoc/>
    public void RemoveTotpAuthenticator(TotpAuthenticator authenticator)
    {
        context.TotpAuthenticators.Remove(authenticator);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordTotpUse(Guid authenticatorId, long timeStep, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "LastUsedTimeStep" < step: the database lets exactly one concurrent request win, and
        // rejects the step of a code that was already used, or an earlier one.
        var updated = await context.TotpAuthenticators
            .Where(authenticator => authenticator.Id == authenticatorId
                && authenticator.ConfirmedAt != null
                && (authenticator.LastUsedTimeStep == null || authenticator.LastUsedTimeStep < timeStep))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(authenticator => authenticator.LastUsedTimeStep, timeStep)
                    .SetProperty(authenticator => authenticator.LastUsedAt, now),
                cancellationToken);

        return updated == 1;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecoveryCode>> GetRecoveryCodes(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.RecoveryCodes
            .Where(code => code.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountUnusedRecoveryCodes(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.RecoveryCodes
            .CountAsync(code => code.UserId == userId && code.UsedAt == null, cancellationToken);
    }

    /// <inheritdoc/>
    public void AddRecoveryCodes(IEnumerable<RecoveryCode> codes)
    {
        context.RecoveryCodes.AddRange(codes);
    }

    /// <inheritdoc/>
    public void RemoveRecoveryCodes(IEnumerable<RecoveryCode> codes)
    {
        context.RecoveryCodes.RemoveRange(codes);
    }

    /// <inheritdoc/>
    public async Task<bool> TryUseRecoveryCode(Guid userId, SecretHash codeHash, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "UsedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.RecoveryCodes
            .Where(code => code.UserId == userId && code.CodeHash == codeHash && code.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.UsedAt, now), cancellationToken);

        return updated == 1;
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
            // Detach the outdated rows, so loading them again returns the current values from the database.
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new ConflictException("The multi-factor authentication of the User was changed by another request. Try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            throw new ConflictException("The multi-factor authentication of the User was changed by another request. Try again.", exception);
        }
    }

    // Folds the Id into the second key of the lock. Two Users may share a key; they then only wait for each other.
    private static int LockKey(Guid userId)
    {
        Span<byte> bytes = stackalloc byte[16];
        userId.TryWriteBytes(bytes);

        var key = 17;

        for (var offset = 0; offset < bytes.Length; offset += sizeof(int))
        {
            key = (key * 31) ^ BitConverter.ToInt32(bytes[offset..]);
        }

        return key;
    }
}
