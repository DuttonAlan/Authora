// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Seals one <see cref="AuditEvent"/> into the hash chain of the audit log (TD-013).
/// </summary>
/// <remarks>
///     The <see cref="Hash"/> covers the <see cref="Sequence"/>, the <see cref="PreviousHash"/> and every field of
///     the event, so changing or deleting a sealed event, or a link, breaks the chain from there on. Links are kept
///     in a table of their own, so the events never need to be updated. Links are only ever added, never changed,
///     and are deleted only by the cleanup after the retention, from the start of the chain.
/// </remarks>
public sealed class AuditChainLink
{
    /// <summary>
    ///     Length of a hash: SHA-256 as lowercase hex.
    /// </summary>
    public const int HashLength = 64;

    /// <summary>
    ///     Used by EF Core to materialize an AuditChainLink from the database.
    /// </summary>
    private AuditChainLink()
    {
    }

    /// <summary>
    ///     Position of the link in the chain, starting at 1 and without gaps.
    /// </summary>
    public long Sequence { get; private set; }

    /// <summary>
    ///     Id of the sealed event. Deliberately no foreign key: a deleted event must leave its link behind.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    ///     The <see cref="Hash"/> of the previous link, or 64 zeros for the first link.
    /// </summary>
    public string PreviousHash { get; private set; } = null!;

    /// <summary>
    ///     SHA-256 over the Sequence, the PreviousHash and the event, as lowercase hex.
    /// </summary>
    public string Hash { get; private set; } = null!;

    /// <summary>
    ///     Point in time (UTC) when the event was sealed.
    /// </summary>
    public DateTime SealedAt { get; private set; }

    /// <summary>
    ///     Creates a link.
    /// </summary>
    /// <param name="sequence">The position in the chain, at least 1.</param>
    /// <param name="eventId">The Id of the sealed event.</param>
    /// <param name="previousHash">The hash of the previous link.</param>
    /// <param name="hash">The hash of this link.</param>
    /// <param name="sealedAt">When the event was sealed (UTC).</param>
    /// <returns>The created link.</returns>
    /// <exception cref="DomainValidationException">Thrown when the Sequence is below 1 or a hash is malformed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sealedAt"/> is not in UTC.</exception>
    public static AuditChainLink Create(long sequence, Guid eventId, string previousHash, string hash, DateTime sealedAt)
    {
        UtcGuard.EnsureUtc(sealedAt);

        if (sequence < 1)
        {
            throw new DomainValidationException("Sequence must be at least 1.", nameof(sequence));
        }

        EnsureHash(previousHash, nameof(previousHash));
        EnsureHash(hash, nameof(hash));

        return new AuditChainLink
        {
            Sequence = sequence,
            EventId = eventId,
            PreviousHash = previousHash,
            Hash = hash,
            SealedAt = sealedAt
        };
    }

    private static void EnsureHash(string value, string paramName)
    {
        if (value is null || value.Length != HashLength || !value.All(char.IsAsciiHexDigitLower))
        {
            throw new DomainValidationException($"A hash must be {HashLength} lowercase hex characters.", paramName);
        }
    }
}
