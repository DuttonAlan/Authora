// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a one-time token in a link that Authora sends by email, e.g. to reset a password.
/// </summary>
/// <remarks>
///     The token itself is a random value without any readable content, and only its hash is stored. It expires,
///     can only be used once, only for its <see cref="Purpose"/>, and only while the User still has the address it
///     was sent to and the same <see cref="User.SecurityStamp"/>, so a password change, deactivation or "sign out
///     everywhere" ends all open links.
/// </remarks>
public sealed class EmailToken
{
    /// <summary>
    ///     Longest allowed lifetime of an email token.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(1);

    /// <summary>
    ///     Used by EF Core to materialize an email token from the database.
    /// </summary>
    private EmailToken()
    {
    }

    /// <summary>
    ///     Id of the email token.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Hash of the token. The plain-text token is only sent in the email and never stored.
    /// </summary>
    public SecretHash TokenHash { get; private set; } = null!;

    /// <summary>
    ///     Id of the <see cref="User"/> the token was sent to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     What the token may be used for.
    /// </summary>
    public EmailTokenPurpose Purpose { get; private set; }

    /// <summary>
    ///     <see cref="User.NormalizedEmail"/> the token was sent to. The token is no longer accepted once the User has
    ///     another address.
    /// </summary>
    public string NormalizedEmail { get; private set; } = null!;

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> of the User when the token was issued. The token is no longer accepted once
    ///     the stamp changes, e.g. because the password was changed or set by an administrator.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token was issued.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token was used or replaced by a newer one, or <see langword="null"/> if it can
    ///     still be used. Set atomically in the database, so a token can only be used once even by concurrent requests.
    /// </summary>
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>
    ///     Creates a new email token.
    /// </summary>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="user">The User the token is sent to.</param>
    /// <param name="purpose">What the token may be used for.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime of the token, at most <see cref="MaxLifetime"/>.</param>
    /// <returns>The created email token.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static EmailToken Create(SecretHash tokenHash, User user, EmailTokenPurpose purpose, DateTime now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentNullException.ThrowIfNull(user);
        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown purpose.");
        }
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lifetime, MaxLifetime);

        return new EmailToken
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            Purpose = purpose,
            NormalizedEmail = user.NormalizedEmail,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the token has expired.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if the token can no longer be used.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsExpired(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return ExpiresAt <= now;
    }

    /// <summary>
    ///     Checks whether the token belongs to a User who still has the address it was sent to and whose
    ///     <see cref="User.SecurityStamp"/> did not change since.
    /// </summary>
    /// <param name="user">The User.</param>
    /// <returns><see langword="true"/> if the token was sent to this User at their current address and stamp.</returns>
    public bool IsFor(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return UserId == user.Id
            && SecurityStamp == user.SecurityStamp
            && string.Equals(NormalizedEmail, user.NormalizedEmail, StringComparison.Ordinal);
    }
}
