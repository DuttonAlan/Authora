// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the passkeys and their challenges.</param>
public sealed class PasskeyRepository(AuthoraContext context) : IPasskeyRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<Passkey>> GetPasskeys(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Passkeys
            .Where(passkey => passkey.UserId == userId)
            .OrderBy(passkey => passkey.CreatedAt)
            .ThenBy(passkey => passkey.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Passkey?> GetPasskey(Guid userId, Guid passkeyId, CancellationToken cancellationToken = default)
    {
        return await context.Passkeys
            .FirstOrDefaultAsync(passkey => passkey.UserId == userId && passkey.Id == passkeyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Passkey?> FindPasskeyByCredentialId(Guid userId, byte[] credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        return await context.Passkeys
            .AsNoTracking()
            .FirstOrDefaultAsync(passkey => passkey.UserId == userId && passkey.CredentialId == credentialId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> HasPasskeys(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Passkeys.AnyAsync(passkey => passkey.UserId == userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CredentialIdExists(byte[] credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialId);

        return await context.Passkeys.AnyAsync(passkey => passkey.CredentialId == credentialId, cancellationToken);
    }

    /// <inheritdoc/>
    public void AddPasskey(Passkey passkey)
    {
        context.Passkeys.Add(passkey);
    }

    /// <inheritdoc/>
    public void RemovePasskeys(IEnumerable<Passkey> passkeys)
    {
        context.Passkeys.RemoveRange(passkeys);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordUse(Guid passkeyId, uint signCount, bool isBackedUp, DateTime now, CancellationToken cancellationToken = default)
    {
        long count = signCount;

        // A single UPDATE ... WHERE "SignCount" < count: the database lets exactly one of two responses with the same
        // counter win. Authenticators that do not count always report 0.
        var updated = await context.Passkeys
            .Where(passkey => passkey.Id == passkeyId
                && (passkey.SignCount < count || (count == 0 && passkey.SignCount == 0)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(passkey => passkey.SignCount, count)
                    .SetProperty(passkey => passkey.IsBackedUp, isBackedUp)
                    .SetProperty(passkey => passkey.LastUsedAt, now),
                cancellationToken);

        return updated == 1;
    }

    /// <inheritdoc/>
    public async Task CreateChallenge(PasskeyChallenge challenge, CancellationToken cancellationToken = default)
    {
        context.PasskeyChallenges.Add(challenge);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsumeChallenge(
        SecretHash challengeHash,
        Guid userId,
        PasskeyCeremony ceremony,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeHash);

        // A single UPDATE ... WHERE "ConsumedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.PasskeyChallenges
            .Where(challenge => challenge.ChallengeHash == challengeHash
                && challenge.UserId == userId
                && challenge.Ceremony == ceremony
                && challenge.ConsumedAt == null
                && challenge.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ConsumedAt, now), cancellationToken);

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

            throw new ConflictException("The passkeys of the User were changed by another request. Try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            throw new ConflictException("The passkey is already registered.", exception);
        }
    }
}
