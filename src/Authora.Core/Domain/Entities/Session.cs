// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents the browser Session of a signed-in User at Authora itself, e.g. used by the /authorize endpoint.
/// </summary>
/// <remarks>
///     The browser only holds a random Session token in a cookie, and only its hash is stored. A Session ends when it
///     is revoked, when its absolute lifetime ends, when it was not used for the idle timeout, or when the
///     <see cref="User.SecurityStamp"/> changes.
/// </remarks>
public sealed class Session
{
    /// <summary>
    ///     Used by EF Core to materialize a Session from the database.
    /// </summary>
    private Session()
    {
    }

    /// <summary>
    ///     Id of the Session.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the signed-in <see cref="User"/>.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     Hash of the Session token in the cookie. The plain-text token is never stored.
    /// </summary>
    public SecretHash TokenHash { get; private set; } = null!;

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> at sign-in. The Session is invalid once the User has a different one.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the User signed in.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Session was last used. Used for the idle timeout.
    /// </summary>
    public DateTime LastActivityAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Session ends, regardless of activity.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Session was revoked, e.g. by signing out, or <see langword="null"/>.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    ///     Creates a new Session for a User who just signed in.
    /// </summary>
    /// <param name="user">The signed-in User.</param>
    /// <param name="tokenHash">The hash of the Session token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The absolute lifetime of the Session.</param>
    /// <returns>The created Session.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC or the lifetime is not positive.</exception>
    public static Session Create(User user, SecretHash tokenHash, DateTime now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tokenHash);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new Session
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            LastActivityAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the Session is neither revoked, expired nor idle for too long.
    /// </summary>
    /// <remarks>
    ///     Does not check the User. Callers must also compare <see cref="SecurityStamp"/> and check <see cref="User.CanSignIn"/>.
    /// </remarks>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="idleTimeout">How long a Session may be unused.</param>
    /// <returns><see langword="true"/> if the Session can be used.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsActive(DateTime now, TimeSpan idleTimeout)
    {
        UtcGuard.EnsureUtc(now);

        return RevokedAt is null && ExpiresAt > now && LastActivityAt + idleTimeout > now;
    }

    /// <summary>
    ///     Records that the Session was used, which extends the idle timeout but not the absolute lifetime.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public void RecordActivity(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        if (now > LastActivityAt)
        {
            LastActivityAt = now;
        }
    }

    /// <summary>
    ///     Revokes the Session. Revoking an already revoked Session keeps the original revocation time.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public void Revoke(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        RevokedAt ??= now;
    }
}
