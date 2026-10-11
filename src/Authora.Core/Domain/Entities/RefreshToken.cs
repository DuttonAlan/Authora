// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Refresh Token, which the Client exchanges for a new Access Token at the /token endpoint.
/// </summary>
/// <remarks>
///     Refresh Tokens are rotated: every use returns a new Refresh Token and consumes the old one. Using a consumed
///     Refresh Token again means that it was stolen, so the whole <see cref="Entities.Grant"/> is revoked
///     (OAuth 2.1, Section 4.3.1). A Refresh Token always has the Scopes of its Grant.
/// </remarks>
public sealed class RefreshToken
{
    /// <summary>
    ///     Used by EF Core to materialize a Refresh Token from the database.
    /// </summary>
    private RefreshToken()
    {
    }

    /// <summary>
    ///     Id of the Refresh Token.
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
    ///     Point in time (UTC) when the token was issued.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the token was exchanged for a new one, or <see langword="null"/> if it was not
    ///     used yet. Set atomically in the database, so a token can only be used once even by concurrent requests.
    /// </summary>
    public DateTime? ConsumedAt { get; private set; }

    /// <summary>
    ///     Creates a new Refresh Token.
    /// </summary>
    /// <param name="grantId">The Id of the Grant the token belongs to.</param>
    /// <param name="tokenHash">The hash of the token.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="lifetime">The lifetime of the token.</param>
    /// <returns>The created Refresh Token.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is missing or invalid.</exception>
    public static RefreshToken Create(Guid grantId, SecretHash tokenHash, DateTime now, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        UtcGuard.EnsureUtc(now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        return new RefreshToken
        {
            GrantId = grantId,
            TokenHash = tokenHash,
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
}
