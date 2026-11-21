// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the pending sign-ins.</param>
public sealed class SignInChallengeRepository(AuthoraContext context) : ISignInChallengeRepository
{
    /// <inheritdoc/>
    public async Task CreateChallenge(SignInChallenge challenge, CancellationToken cancellationToken = default)
    {
        context.SignInChallenges.Add(challenge);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SignInChallenge?> GetChallengeByTokenHash(SecretHash tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.SignInChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.TokenHash == tokenHash, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default)
    {
        // A single UPDATE ... WHERE "ConsumedAt" IS NULL: the database lets exactly one concurrent request win.
        var updated = await context.SignInChallenges
            .Where(challenge => challenge.Id == id && challenge.ConsumedAt == null && challenge.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ConsumedAt, now), cancellationToken);

        return updated == 1;
    }
}
