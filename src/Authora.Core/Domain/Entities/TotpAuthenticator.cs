// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     The authenticator app of a User: the secret of its time-based one-time passwords (TOTP, RFC 6238).
/// </summary>
/// <remarks>
///     <para>
///         A User has at most one. It is created unconfirmed when the User starts the setup and only counts as a
///         second factor once the User entered a first valid code (<see cref="ConfirmedAt"/>). An unconfirmed one is
///         replaced when the User starts again, and can no longer be confirmed after <see cref="EnrollmentLifetime"/>.
///     </para>
///     <para>
///         The secret is stored encrypted (<see cref="ProtectedSecret"/>), since the server needs it in plain text to
///         compute the codes. <see cref="LastUsedTimeStep"/> rejects a code whose time step was already used.
///     </para>
/// </remarks>
public sealed class TotpAuthenticator
{
    /// <summary>
    ///     How long the User has to confirm the setup with a first code.
    /// </summary>
    public static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Maximum length of <see cref="ProtectedSecret"/>.
    /// </summary>
    public const int ProtectedSecretMaxLength = 1024;

    /// <summary>
    ///     Used by EF Core to materialize an authenticator from the database.
    /// </summary>
    private TotpAuthenticator()
    {
    }

    /// <summary>
    ///     Id of the authenticator.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="User"/> it belongs to.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     The secret, encrypted with Data Protection. Never stored or logged in plain text.
    /// </summary>
    public string ProtectedSecret { get; private set; } = null!;

    /// <summary>
    ///     Point in time (UTC) when the User started the setup.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the User confirmed the setup with a first code, or <see langword="null"/> while it
    ///     is not confirmed and does not count as a second factor.
    /// </summary>
    public DateTime? ConfirmedAt { get; private set; }

    /// <summary>
    ///     The time step of the last accepted code. Codes of this or an earlier step are rejected.
    /// </summary>
    public long? LastUsedTimeStep { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when a code was last accepted, including the one that confirmed the setup.
    /// </summary>
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>
    ///     Whether the setup is confirmed, so the User must enter a code after the password.
    /// </summary>
    public bool IsConfirmed => ConfirmedAt is not null;

    /// <summary>
    ///     Creates an unconfirmed authenticator.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="protectedSecret">The encrypted secret.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The authenticator.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static TotpAuthenticator Create(Guid userId, string protectedSecret, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(protectedSecret.Length, ProtectedSecretMaxLength);
        UtcGuard.EnsureUtc(now);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The User is required.", nameof(userId));
        }

        return new TotpAuthenticator
        {
            UserId = userId,
            ProtectedSecret = protectedSecret,
            CreatedAt = now
        };
    }

    /// <summary>
    ///     Checks whether the setup can still be confirmed.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if it is unconfirmed and was started less than <see cref="EnrollmentLifetime"/> ago.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool CanBeConfirmed(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return !IsConfirmed && CreatedAt + EnrollmentLifetime > now;
    }

    /// <summary>
    ///     Starts the setup again with a new secret, e.g. after the previous one expired.
    /// </summary>
    /// <param name="protectedSecret">The new encrypted secret.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the setup is already confirmed.</exception>
    public void Restart(string protectedSecret, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(protectedSecret.Length, ProtectedSecretMaxLength);
        UtcGuard.EnsureUtc(now);

        if (IsConfirmed)
        {
            throw new InvalidOperationException("A confirmed authenticator cannot be set up again; remove it first.");
        }

        ProtectedSecret = protectedSecret;
        CreatedAt = now;
    }

    /// <summary>
    ///     Confirms the setup with the first valid code.
    /// </summary>
    /// <param name="timeStep">The time step of the code, which cannot be used again.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the setup is already confirmed or expired.</exception>
    public void Confirm(long timeStep, DateTime now)
    {
        if (!CanBeConfirmed(now))
        {
            throw new InvalidOperationException("The setup is already confirmed or expired.");
        }

        ConfirmedAt = now;
        LastUsedTimeStep = timeStep;
        LastUsedAt = now;
    }
}
