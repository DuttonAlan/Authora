// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract to read the hash chain of the audit log. Links are written by the sealing in
///     Infrastructure, never through this contract.
/// </summary>
public interface IAuditChainRepository
{
    /// <summary>
    ///     Gets the next links of the chain with the events they seal.
    /// </summary>
    /// <param name="afterSequence">Only links with a greater Sequence.</param>
    /// <param name="count">The maximum number of links.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The links ordered by Sequence, each with its event, or null if the event no longer exists.</returns>
    public Task<IReadOnlyList<(AuditChainLink Link, AuditEvent? Event)>> GetLinks(
        long afterSequence,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the link with a Sequence, or null if there is none.
    /// </summary>
    /// <param name="sequence">The Sequence.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The link, or null.</returns>
    public Task<AuditChainLink?> GetLink(long sequence, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the events that occurred before a point in time and are not sealed.
    /// </summary>
    /// <param name="occurredBefore">The point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of events.</returns>
    public Task<int> CountUnsealedEvents(DateTime occurredBefore, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Whether the cleanup recorded that the chain starts at a Sequence, with an event of the type
    ///     <c>system.audit-chain-truncated</c>.
    /// </summary>
    /// <param name="firstSequence">The Sequence the chain starts at.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if such an event exists.</returns>
    public Task<bool> IsTruncationRecorded(long firstSequence, CancellationToken cancellationToken = default);
}
