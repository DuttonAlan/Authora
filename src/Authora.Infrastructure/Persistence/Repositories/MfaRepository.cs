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
}
