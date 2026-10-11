// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents the approval of a User for a Client, created when an Authorization Code is exchanged.
///     All Access and Refresh Tokens issued from the code and from its Refresh Tokens belong to the same Grant.
/// </summary>
/// <remarks>
///     The Grant groups the tokens into one family, so they can be revoked together: when a Refresh Token is revoked
///     or reused, when an Authorization Code is used twice, or when the Client is disabled.
///     A token is only valid while its Grant is not revoked.
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
    ///     Id of the <see cref="User"/> who approved the request.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    ///     <see cref="User.SecurityStamp"/> when the User approved the request. All tokens of the Grant are invalid
    ///     once the User has a different one, e.g. after "sign out everywhere" or a password change.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>
    ///     The approved Scopes, space-delimited. Refresh Tokens always have these Scopes,
    ///     Access Tokens can have fewer.
    /// </summary>
    public string Scope { get; private set; } = null!;

    /// <summary>
    ///     Id of the <see cref="Entities.AuthorizationCode"/> the Grant was created from.
    /// </summary>
    public Guid AuthorizationCodeId { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Grant was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

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
    ///     Creates a new Grant from an Authorization Code that is being exchanged.
    /// </summary>
    /// <param name="authorizationCode">The exchanged Authorization Code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The created Grant.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public static Grant Create(AuthorizationCode authorizationCode, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(authorizationCode);
        UtcGuard.EnsureUtc(now);

        return new Grant
        {
            ClientId = authorizationCode.ClientId,
            UserId = authorizationCode.UserId,
            SecurityStamp = authorizationCode.SecurityStamp,
            Scope = authorizationCode.Scope,
            AuthorizationCodeId = authorizationCode.Id,
            CreatedAt = now
        };
    }
}
