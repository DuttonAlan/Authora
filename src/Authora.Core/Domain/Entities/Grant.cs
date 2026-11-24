// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents the approval of a User for a Client, created when an Authorization Code is exchanged.
///     All Access and Refresh Tokens issued from the code and from its Refresh Tokens belong to the same Grant.
///     A Grant of the Client Credentials Grant Type has no User: the Client acts for itself, and the Grant holds a
///     single Access Token.
/// </summary>
/// <remarks>
///     The Grant groups the tokens into one family, so they can be revoked together: when a Refresh Token is revoked
///     or reused, when an Authorization Code is used twice, when the Client is disabled, or when the Session the Grant
///     was approved in ends.
///     A token is only valid while its Grant is neither revoked nor expired. The Grant has an absolute lifetime, so
///     rotating Refresh Tokens cannot keep it alive forever: no token of the Grant outlives <see cref="ExpiresAt"/>.
/// </remarks>
public sealed class Grant
{
    /// <summary>
    ///     Used by EF Core to materialize a Grant from the database.
    /// </summary>
    private Grant()
    {
    }

    /// <summary>
    ///     Id of the Grant.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="Client"/> the tokens are issued to.
    /// </summary>
    public Guid ClientId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="User"/> who approved the request, or <see langword="null"/> for a Grant of the Client
    ///     itself (<see cref="Enums.GrantTypes.ClientCredentials"/>).
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> when the User approved the request, or <see langword="null"/> without a User.
    ///     All tokens of the Grant are invalid once the User has a different one, e.g. after "sign out everywhere" or a
    ///     password change.
    /// </summary>
    public Guid? SecurityStamp { get; private set; }

    /// <summary>
    ///     The approved Scopes, space-delimited. Refresh Tokens always have these Scopes,
    ///     Access Tokens can have fewer.
    /// </summary>
    public string Scope { get; private set; } = null!;

    /// <summary>
    ///     The approved resources (RFC 8707), as Ids of the APIs and <see cref="AuthoraResource.Id"/> for Authora.
    ///     Access Tokens of the Grant are meant for these resources or a part of them. Empty if the request named
    ///     none: then a token is meant for every Client that serves one of its Scopes, unless the token request names
    ///     resources.
    /// </summary>
    public IReadOnlyList<Guid> Resources { get; private set; } = [];

    /// <summary>
    ///     Id of the <see cref="Entities.AuthorizationCode"/> the Grant was created from, or <see langword="null"/> for a
    ///     Grant of the Client itself.
    /// </summary>
    public Guid? AuthorizationCodeId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="Session"/> in which the User approved the request, or <see langword="null"/> for a Grant
    ///     of the Client itself and for Grants from before it was recorded. When the Session ends, the Grant is revoked
    ///     and its Client notified (OpenID Connect Back-Channel Logout 1.0).
    /// </summary>
    /// <remarks>
    ///     Not a foreign key: the cleanup may delete an ended Session before the Grant.
    /// </remarks>
    public Guid? SessionId { get; private set; }

    /// <summary>
    ///     The authentication methods the User completed in the Session the Grant was approved in
    ///     (<see cref="Domain.AuthenticationMethods"/>), space-delimited, e.g. <c>pwd otp</c>. Every token of the Grant
    ///     carries them, so the admin API can check how the User signed in. <see langword="null"/> for a Grant of the
    ///     Client itself, and for Grants from before the methods were recorded, which count as a sign-in with the
    ///     password only.
    /// </summary>
    public string? AuthenticationMethod { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Grant was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Grant expires. No token of the Grant expires later, so the User has to approve
    ///     the Client again afterwards.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Grant was revoked, or <see langword="null"/> if it is not revoked.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    ///     The approved Scope Names.
    /// </summary>
    public IReadOnlyList<string> Scopes => ScopeList.Split(Scope);

    /// <summary>
    ///     Whether the Grant was revoked, which makes all of its tokens invalid.
    /// </summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    ///     Whether the Grant belongs to a User. False for a Grant of the Client itself.
    /// </summary>
    public bool HasUser => UserId is not null;

    /// <summary>
    ///     The authentication methods of the sign-in, empty without them, see <see cref="AuthenticationMethod"/>.
    /// </summary>
    public IReadOnlyList<string> AuthenticationMethods => AuthenticationMethod is null
        ? []
        : Domain.AuthenticationMethods.Split(AuthenticationMethod);

    /// <summary>
    ///     Creates a new Grant from an Authorization Code that is being exchanged.
    /// </summary>
    /// <param name="authorizationCode">The exchanged Authorization Code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The absolute lifetime of the Grant.</param>
    /// <returns>The created Grant.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lifetime"/> is not positive.</exception>
    public static Grant Create(AuthorizationCode authorizationCode, DateTime now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(authorizationCode);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new Grant
        {
            ClientId = authorizationCode.ClientId,
            UserId = authorizationCode.UserId,
            SecurityStamp = authorizationCode.SecurityStamp,
            Scope = authorizationCode.Scope,
            Resources = authorizationCode.Resources.ToList(),
            AuthorizationCodeId = authorizationCode.Id,
            SessionId = authorizationCode.SessionId,
            AuthenticationMethod = authorizationCode.AuthenticationMethod,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Creates a Grant of a Client for itself (<see cref="Enums.GrantTypes.ClientCredentials"/>), without a User.
    ///     It ends with its only Access Token, so <paramref name="lifetime"/> is the Access Token lifetime.
    /// </summary>
    /// <param name="client">The Client that obtains the token.</param>
    /// <param name="scopes">The granted Scope Names.</param>
    /// <param name="resources">The Ids of the resources named with <c>resource</c>, or empty.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime of the Grant.</param>
    /// <returns>The created Grant.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lifetime"/> is not positive.</exception>
    public static Grant CreateForClient(
        Client client,
        IEnumerable<string> scopes,
        IEnumerable<Guid> resources,
        DateTime now,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(resources);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new Grant
        {
            ClientId = client.Id,
            Scope = ScopeList.Format(scopes),
            Resources = resources.Distinct().ToList(),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the Grant has expired.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if no token of the Grant can be used anymore.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsExpired(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return ExpiresAt <= now;
    }

    /// <summary>
    ///     Limits the lifetime of a new token of the Grant, so the token does not outlive the Grant.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime the token would have.</param>
    /// <returns>The lifetime, at most the time left until <see cref="ExpiresAt"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public TimeSpan LimitTokenLifetime(DateTime now, TimeSpan lifetime)
    {
        UtcGuard.EnsureUtc(now);

        var remaining = ExpiresAt - now;

        return lifetime < remaining ? lifetime : remaining;
    }
}
