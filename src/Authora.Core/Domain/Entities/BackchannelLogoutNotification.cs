// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     A Logout Token that is still to be sent to the Back-Channel Logout URI of a Client (OpenID Connect Back-Channel
///     Logout 1.0), because a Session ended in which the Client received tokens.
/// </summary>
/// <remarks>
///     <para>
///         The notifications form an outbox in the database: they are written in the transaction that ends the
///         Session, so none is lost or sent for a change that was rolled back, and they survive restarts. A background
///         service on any instance sends them, retries failed deliveries with a growing delay and drops a notification
///         once it was delivered or gives up.
///     </para>
///     <para>
///         A notification holds everything the Logout Token needs, so it does not depend on the Session or the Grants,
///         which the cleanup may delete in the meantime. The Logout Token itself is created when it is sent, so it is
///         signed with the current key and never stored.
///     </para>
/// </remarks>
public sealed class BackchannelLogoutNotification
{
    /// <summary>
    ///     Maximum length of <see cref="LastError"/>; longer messages are cut.
    /// </summary>
    public const int LastErrorMaxLength = 500;

    /// <summary>
    ///     Used by EF Core to materialize a notification from the database.
    /// </summary>
    private BackchannelLogoutNotification()
    {
    }

    /// <summary>
    ///     Id of the notification.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="Client"/> to notify, the <c>aud</c> of the Logout Token.
    /// </summary>
    public Guid ClientId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="User"/> who was signed out, the <c>sub</c> of the Logout Token.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     <see cref="Session.Sid"/> of the Session that ended, the <c>sid</c> of the Logout Token.
    /// </summary>
    public string Sid { get; private set; } = null!;

    /// <summary>
    ///     Number of failed deliveries so far.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    ///     Point in time (UTC) from which the notification is (again) due. While an instance sends it, this is the end
    ///     of its claim, after which another instance may take it over.
    /// </summary>
    public DateTime NextAttemptAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Session ended and the notification was written.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Why the last delivery failed, e.g. the HTTP status code, or <see langword="null"/> before the first failure.
    ///     Never contains the Logout Token.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    ///     The <c>sub</c> of the Logout Token.
    /// </summary>
    public string Subject => UserId.ToString("D");

    /// <summary>
    ///     Creates a notification that is due right away.
    /// </summary>
    /// <param name="clientId">Id of the Client to notify.</param>
    /// <param name="userId">Id of the User who was signed out.</param>
    /// <param name="sid">The Session ID of the Session that ended.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The created notification.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or <paramref name="now"/> is not in UTC.</exception>
    public static BackchannelLogoutNotification Create(Guid clientId, Guid userId, string sid, DateTime now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentException.ThrowIfNullOrEmpty(sid);
        UtcGuard.EnsureUtc(now);

        return new BackchannelLogoutNotification
        {
            ClientId = clientId,
            UserId = userId,
            Sid = sid,
            CreatedAt = now,
            NextAttemptAt = now
        };
    }

    /// <summary>
    ///     Records a failed delivery and when to try again.
    /// </summary>
    /// <param name="error">Why the delivery failed. Cut to <see cref="LastErrorMaxLength"/>.</param>
    /// <param name="nextAttemptAt">When the notification is due again (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nextAttemptAt"/> is not in UTC.</exception>
    public void RecordFailure(string error, DateTime nextAttemptAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        UtcGuard.EnsureUtc(nextAttemptAt);

        Attempts++;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
        NextAttemptAt = nextAttemptAt;
    }
}
