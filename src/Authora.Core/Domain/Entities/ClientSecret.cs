// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Secret a Confidential <see cref="Client"/> uses to authenticate at the /token endpoint.
/// </summary>
/// <remarks>
///     A Client can have multiple active Secrets at once to allow rotation without downtime.
///     Secrets are created and revoked through the <see cref="Client"/>.
/// </remarks>
public sealed class ClientSecret
{
    /// <summary>
    ///     Id of the Secret.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Id of the <see cref="Client"/> the Secret belongs to. Set by EF Core when the Secret is saved.
    /// </summary>
    public Guid ClientId { get; private set; }

    /// <summary>
    ///     Hash of the Secret. The plain-text Secret is only shown once when it is created and never stored.
    /// </summary>
    public required string SecretHash { get; init; }

    /// <summary>
    ///     Point in time (UTC) when the Secret was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Secret expires, or <see langword="null"/> if it never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Point in time (UTC) when the Secret was revoked, or <see langword="null"/> if it is not revoked.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    ///     Checks whether the Secret can be used, i.e. it is neither revoked nor expired.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if the Secret is active, otherwise <see langword="false"/>.</returns>
    public bool IsActive(DateTime now)
    {
        return RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
    }

    /// <summary>
    ///     Revokes the Secret. Revoking an already revoked Secret keeps the original revocation time.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    public void Revoke(DateTime now)
    {
        RevokedAt ??= now;
    }
}
