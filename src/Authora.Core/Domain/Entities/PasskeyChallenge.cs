// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     The random challenge of a WebAuthn ceremony: the browser has the authenticator sign it, so a response proves it
///     was made for this ceremony and not replayed.
/// </summary>
/// <remarks>
///     Only its hash is stored. It belongs to one User and one <see cref="PasskeyCeremony"/>, is short-lived and
///     accepted at most once: it is consumed in a single atomic update before the response is verified, whatever the
///     outcome.
/// </remarks>
public sealed class PasskeyChallenge
{
    /// <summary>
    ///     How long the User has to complete the ceremony, which is also the timeout the browser gets.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Length of a challenge in bytes. WebAuthn asks for at least 16.
    /// </summary>
    public const int Length = 32;

    /// <summary>
    ///     Used by EF Core to materialize a challenge from the database.
    /// </summary>
    private PasskeyChallenge()
    {
    }

    /// <summary>
    ///     Id of the challenge.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Hash of the challenge. The challenge itself is never stored.
    /// </summary>
    public SecretHash ChallengeHash { get; private set; } = null!;

    /// <summary>
    ///     Id of the <see cref="User"/> the ceremony is for.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     The ceremony the challenge is for.
    /// </summary>
    public PasskeyCeremony Ceremony { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the challenge was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the challenge expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when a response to the challenge was received, or <see langword="null"/> while it can
    ///     still be used. Set atomically in the database, so it is accepted at most once.
    /// </summary>
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>
    ///     Creates a challenge.
    /// </summary>
    /// <param name="challengeHash">The hash of the challenge.</param>
    /// <param name="userId">Id of the User the ceremony is for.</param>
    /// <param name="ceremony">The ceremony.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The created challenge, valid for <see cref="Lifetime"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or <paramref name="now"/> is not in UTC.</exception>
    public static PasskeyChallenge Create(SecretHash challengeHash, Guid userId, PasskeyCeremony ceremony, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(challengeHash);
        UtcGuard.EnsureUtc(now);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The User is required.", nameof(userId));
        }

        return new PasskeyChallenge
        {
            ChallengeHash = challengeHash,
            UserId = userId,
            Ceremony = ceremony,
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };
    }
}
