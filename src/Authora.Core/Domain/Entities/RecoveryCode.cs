// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Mfa;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     A single-use recovery code, with which a User signs in instead of with their authenticator app, e.g. after
///     losing their phone.
/// </summary>
/// <remarks>
///     A User gets a set of <see cref="RecoveryCodeFormat.CountPerSet"/> together with their authenticator app. Only
///     the hash of a code is stored; the User sees the codes once. Used codes are kept with <see cref="UsedAt"/> until
///     the set is replaced, so the last use can be shown.
/// </remarks>
public sealed class RecoveryCode
{
    /// <summary>
    ///     Used by EF Core to materialize a recovery code from the database.
    /// </summary>
    private RecoveryCode()
    {
    }

    /// <summary>
    ///     Id of the recovery code.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="User"/> it belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     Hash of the normalized code (<see cref="RecoveryCodeFormat.TryNormalize"/>). The plain-text code is never stored.
    /// </summary>
    public SecretHash CodeHash { get; private set; } = null!;

    /// <summary>
    ///     Point in time (UTC) when the set was generated.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the code was used, or <see langword="null"/> while it can still be used. Set
    ///     atomically in the database, so a code can only be used once even by concurrent requests.
    /// </summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>
    ///     Creates an unused recovery code.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="codeHash">Hash of the normalized code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The recovery code.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static RecoveryCode Create(Guid userId, SecretHash codeHash, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(codeHash);
        UtcGuard.EnsureUtc(now);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The User is required.", nameof(userId));
        }

        return new RecoveryCode
        {
            UserId = userId,
            CodeHash = codeHash,
            CreatedAt = now
        };
    }
}
