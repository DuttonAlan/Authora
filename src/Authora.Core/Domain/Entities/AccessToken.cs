// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents an Access Token. Access Tokens are opaque reference tokens: a random value without readable
///     content, which resource servers validate through the introspection endpoint (RFC 7662).
/// </summary>
/// <remarks>
///     Because every validation asks Authora, revoking the token or its <see cref="Entities.Grant"/>, disabling the
///     Client or rotating the <see cref="User.SecurityStamp"/> takes effect immediately.
/// </remarks>
public sealed class AccessToken
{
    /// <summary>
    ///     Used by EF Core to materialize an Access Token from the database.
    /// </summary>
    private AccessToken()
    {
    }

    /// <summary>
    ///     Id of the Access Token.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="Entities.Grant"/> the token belongs to.
    /// </summary>
    public Guid GrantId { get; private set; }

    /// <summary>
    ///     The <see cref="Entities.Grant"/> the token belongs to. Only set when it was loaded with the token.
    /// </summary>
    public Grant? Grant { get; private set; }

    /// <summary>
    ///     Hash of the token. The plain-text token is only sent to the Client and never stored.
    /// </summary>
    public SecretHash TokenHash { get; private set; } = null!;

    /// <summary>
    ///     The Scopes of the token, space-delimited. Always a subset of the Scopes of the Grant.
    /// </summary>
    public string Scope { get; private set; } = null!;

    /// <summary>
    ///     Point in time (UTC) when the token was issued.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token was revoked, or <see langword="null"/> if it is not revoked.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    ///     The Scope Names of the token.
    /// </summary>
    public IReadOnlyList<string> Scopes => ScopeList.Split(Scope);

    /// <summary>
    ///     Creates a new Access Token.
    /// </summary>
    /// <param name="grantId">The Id of the Grant the token belongs to.</param>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="scopes">The Scope Names of the token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime of the token.</param>
    /// <returns>The created Access Token.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static AccessToken Create(Guid grantId, SecretHash tokenHash, IEnumerable<string> scopes, DateTime now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new AccessToken
        {
            GrantId = grantId,
            TokenHash = tokenHash,
            Scope = ScopeList.Format(scopes),
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>
    ///     Checks whether the token itself is neither revoked nor expired.
    /// </summary>
    /// <remarks>
    ///     Does not check the Grant, the Client or the User. Callers must check them as well.
    /// </remarks>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if the token is active.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsActive(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return RevokedAt is null && ExpiresAt > now;
    }
}
