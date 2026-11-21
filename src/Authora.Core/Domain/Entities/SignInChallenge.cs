// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a pending sign-in: the User completed some authentication methods, e.g. the password, and must
///     complete a second factor before the sign-in is finished.
/// </summary>
/// <remarks>
///     <para>
///         The browser only holds a random token in a cookie of its own, not the Session cookie, and only its hash is
///         stored. A pending sign-in is short-lived, can only be completed once, and only while the User has the same
///         <see cref="User.SecurityStamp"/>, so a password reset or "sign out everywhere" ends it.
///     </para>
///     <para>
///         Without <see cref="SessionId"/>, completing it creates a new Session. With it, it is a step-up: the User
///         completes an additional factor for that existing Session, which then gets a new Session token.
///     </para>
/// </remarks>
public sealed class SignInChallenge
{
    /// <summary>
    ///     Longest allowed lifetime of a pending sign-in.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Used by EF Core to materialize a pending sign-in from the database.
    /// </summary>
    private SignInChallenge()
    {
    }

    /// <summary>
    ///     Id of the pending sign-in.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Hash of the token in the cookie. The plain-text token is never stored.
    /// </summary>
    public SecretHash TokenHash { get; private set; } = null!;

    /// <summary>
    ///     Id of the <see cref="User"/> who is signing in.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> when the sign-in started. It cannot be completed once the User has a
    ///     different one.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>
    ///     Id of the <see cref="Session"/> a step-up is for, or <see langword="null"/> for a new sign-in.
    /// </summary>
    /// <remarks>
    ///     Not a foreign key: the Session may end and be deleted before the pending sign-in.
    /// </remarks>
    public Guid? SessionId { get; private set; }

    /// <summary>
    ///     The authentication methods already completed (<see cref="Domain.AuthenticationMethods"/>), e.g. <c>pwd</c>.
    /// </summary>
    public string AuthenticationMethod { get; private set; } = null!;

    /// <summary>
    ///     Point in time (UTC) when the sign-in started.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the pending sign-in expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the sign-in was completed, or <see langword="null"/> while it is pending. Set
    ///     atomically in the database, so it can only be completed once even by concurrent requests.
    /// </summary>
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>
    ///     The authentication methods already completed.
    /// </summary>
    public IReadOnlyList<string> AuthenticationMethods => Domain.AuthenticationMethods.Split(AuthenticationMethod);

    /// <summary>
    ///     Creates a pending sign-in for a User who completed the given methods.
    /// </summary>
    /// <param name="tokenHash">The hash of the token in the cookie.</param>
    /// <param name="user">The User who is signing in.</param>
    /// <param name="authenticationMethods">The methods the User completed so far.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime, at most <see cref="MaxLifetime"/>.</param>
    /// <param name="session">The Session of a step-up, or null for a new sign-in.</param>
    /// <returns>The created pending sign-in.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid, or the Session belongs to another User.</exception>
    public static SignInChallenge Create(
        SecretHash tokenHash,
        User user,
        IEnumerable<string> authenticationMethods,
        DateTime now,
        TimeSpan lifetime,
        Session? session = null)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentNullException.ThrowIfNull(user);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lifetime, MaxLifetime);

        if (session is not null && session.UserId != user.Id)
        {
            throw new ArgumentException("The Session belongs to another User.", nameof(session));
        }

        return new SignInChallenge
        {
            TokenHash = tokenHash,
            UserId = user.Id,
            SecurityStamp = user.SecurityStamp,
            SessionId = session?.Id,
            AuthenticationMethod = Domain.AuthenticationMethods.Format(authenticationMethods),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the pending sign-in has expired.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if it can no longer be completed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsExpired(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return ExpiresAt <= now;
    }

    /// <summary>
    ///     Checks whether the pending sign-in belongs to the User and their <see cref="User.SecurityStamp"/> did not
    ///     change since it started.
    /// </summary>
    /// <param name="user">The User.</param>
    /// <returns><see langword="true"/> if the User can complete it.</returns>
    public bool IsFor(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return UserId == user.Id && SecurityStamp == user.SecurityStamp;
    }
}
