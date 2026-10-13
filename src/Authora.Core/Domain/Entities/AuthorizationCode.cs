// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents an Authorization Code issued by the /authorize endpoint, which the Client exchanges for tokens at
///     the /token endpoint.
/// </summary>
/// <remarks>
///     The code itself is a random value without any readable content, and only its hash is stored. It is short-lived
///     and can only be used once. It is bound to the Client, the User, the Redirect URI, the approved Scopes and the
///     PKCE code challenge of the authorization request.
/// </remarks>
public sealed class AuthorizationCode
{
    /// <summary>
    ///     Longest allowed lifetime of an Authorization Code (10 minutes, RFC 6749, Section 4.1.2).
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Maximum length of the OpenID Connect <c>nonce</c>.
    /// </summary>
    public const int NonceMaxLength = 512;

    /// <summary>
    ///     Used by EF Core to materialize an Authorization Code from the database.
    /// </summary>
    private AuthorizationCode()
    {
    }

    /// <summary>
    ///     Id of the Authorization Code.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Hash of the code. The plain-text code is only sent to the Client and never stored.
    /// </summary>
    public SecretHash CodeHash { get; private set; } = null!;

    /// <summary>
    ///     Id of the <see cref="Client"/> the code was issued to.
    /// </summary>
    public Guid ClientId { get; private set; }

    /// <summary>
    ///     Id of the <see cref="User"/> who approved the request.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> when the code was issued. The code cannot be exchanged once the User has a
    ///     different one.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>
    ///     The <c>redirect_uri</c> exactly as sent in the authorization request, or <see langword="null"/> if it was
    ///     omitted. If it was sent, the token request must send the same value.
    /// </summary>
    public string? RedirectUri { get; private set; }

    /// <summary>
    ///     The approved Scopes, space-delimited.
    /// </summary>
    public string Scope { get; private set; } = null!;

    /// <summary>
    ///     The PKCE code challenge of the authorization request. The method is always <c>S256</c>.
    /// </summary>
    public string CodeChallenge { get; private set; } = null!;

    /// <summary>
    ///     The OpenID Connect <c>nonce</c> of the authorization request, returned unchanged in the ID Token, or
    ///     <see langword="null"/> if it was not sent.
    /// </summary>
    public string? Nonce { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the User signed in, returned as <c>auth_time</c> in the ID Token.
    /// </summary>
    public DateTime AuthenticatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the code was issued.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the code expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the code was exchanged, or <see langword="null"/> if it was not used yet.
    ///     Set atomically in the database, so a code can only be exchanged once even by concurrent requests.
    /// </summary>
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the code was used a second time, or <see langword="null"/> if it was not reused.
    ///     Set atomically in the database. The request that exchanged the code checks it after saving its Grant, so the
    ///     Grant is revoked even if the reuse was detected before the Grant existed.
    /// </summary>
    public DateTime? ReusedAt { get; private set; }

    /// <summary>
    ///     The approved Scope Names.
    /// </summary>
    public IReadOnlyList<string> Scopes => ScopeList.Split(Scope);

    /// <summary>
    ///     Creates a new Authorization Code.
    /// </summary>
    /// <param name="codeHash">The hash of the code.</param>
    /// <param name="client">The Client the code is issued to.</param>
    /// <param name="user">The User who approved the request.</param>
    /// <param name="redirectUri">The <c>redirect_uri</c> as sent in the request, or null if it was omitted.</param>
    /// <param name="scopes">The approved Scope Names.</param>
    /// <param name="codeChallenge">The PKCE code challenge (method <c>S256</c>).</param>
    /// <param name="nonce">The OpenID Connect <c>nonce</c>, or null if it was not sent.</param>
    /// <param name="authenticatedAt">When the User signed in (UTC).</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime of the code, at most <see cref="MaxLifetime"/>.</param>
    /// <returns>The created Authorization Code.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static AuthorizationCode Create(
        SecretHash codeHash,
        Client client,
        User user,
        string? redirectUri,
        IEnumerable<string> scopes,
        string codeChallenge,
        string? nonce,
        DateTime authenticatedAt,
        DateTime now,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(codeHash);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeChallenge);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nonce?.Length ?? 0, NonceMaxLength, nameof(nonce));
        UtcGuard.EnsureUtc(authenticatedAt);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lifetime, MaxLifetime);

        return new AuthorizationCode
        {
            CodeHash = codeHash,
            ClientId = client.Id,
            UserId = user.Id,
            SecurityStamp = user.SecurityStamp,
            RedirectUri = redirectUri,
            Scope = ScopeList.Format(scopes),
            CodeChallenge = codeChallenge,
            Nonce = nonce,
            AuthenticatedAt = authenticatedAt,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the code has expired.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if the code can no longer be exchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsExpired(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return ExpiresAt <= now;
    }
}
