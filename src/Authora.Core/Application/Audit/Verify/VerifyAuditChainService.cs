// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit.Chain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Audit.Verify;

/// <summary>
///     The Service to verify the hash chain of the audit log (TD-013).
/// </summary>
/// <remarks>
///     <para>
///         Recomputes every link from the first remaining one: the Sequence must have no gaps, each link must continue
///         the hash of the previous one, and its event must still exist and hash to the stored value. The first
///         remaining link is trusted, because older links are deleted after the retention.
///     </para>
///     <para>
///         Someone with write access to the database can still rebuild the whole chain after a change. That is only
///         detected by comparing with a checkpoint (Sequence and hash) from the application logs, which the sealing
///         writes after every batch and which should be shipped to a separate system.
///     </para>
/// </remarks>
/// <param name="auditChainRepository">An Instance of a IAuditChainRepository to read the chain.</param>
/// <param name="options">The sealing interval and the batch size.</param>
/// <param name="timeProvider">The clock, to find events that should be sealed by now.</param>
/// <param name="logger">The Logger to record a broken chain.</param>
public sealed class VerifyAuditChainService(
    IAuditChainRepository auditChainRepository,
    IOptions<AuditChainOptions> options,
    TimeProvider timeProvider,
    ILogger<VerifyAuditChainService> logger)
{
    // Time the sealing gets beyond its interval, e.g. for a slow run, before an unsealed event counts as a problem.
    private static readonly TimeSpan SealGrace = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Verifies the chain and optionally a checkpoint from the application logs.
    /// </summary>
    /// <param name="checkpointSequence">The Sequence of a checkpoint, or null.</param>
    /// <param name="checkpointHash">The hash of the checkpoint, or null. Required with a Sequence.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The result.</returns>
    /// <exception cref="DomainValidationException">Thrown when only one part of the checkpoint is given.</exception>
    public async Task<AuditChainVerificationResponse> ExecuteAsync(
        long? checkpointSequence,
        string? checkpointHash,
        CancellationToken cancellationToken = default)
    {
        if (checkpointSequence is null != string.IsNullOrEmpty(checkpointHash))
        {
            throw new DomainValidationException("A checkpoint needs both its Sequence and its hash.", nameof(checkpointHash));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batchSize = options.Value.BatchSize;

        AuditChainLink? first = null;
        AuditChainLink? previous = null;
        long checkedLinks = 0;
        (long Sequence, string Problem)? broken = null;

        while (broken is null)
        {
            var links = await auditChainRepository.GetLinks(previous?.Sequence ?? 0, batchSize, cancellationToken);

            foreach (var (link, auditEvent) in links)
            {
                broken = Check(previous, link, auditEvent);

                if (broken is not null)
                {
                    break;
                }

                first ??= link;
                previous = link;
                checkedLinks++;
            }

            if (links.Count < batchSize)
            {
                break;
            }
        }

        var unsealed = await auditChainRepository.CountUnsealedEvents(
            now - options.Value.SealInterval - SealGrace,
            cancellationToken);

        bool? checkpointMatches = null;

        if (checkpointSequence is not null && broken is null)
        {
            checkpointMatches = await MatchesCheckpointAsync(checkpointSequence.Value, checkpointHash!, first, previous, cancellationToken);
        }

        var problem = broken?.Problem
            ?? (unsealed > 0 ? $"{unsealed} event(s) are not sealed, although they should be by now." : null)
            ?? (checkpointMatches == false ? $"The chain does not match the checkpoint at Sequence {checkpointSequence}." : null);

        if (problem is not null)
        {
            logger.AuditChainBroken(broken?.Sequence, problem);
        }

        return new AuditChainVerificationResponse(
            IsIntact: problem is null,
            FirstSequence: first?.Sequence,
            LastSequence: previous?.Sequence,
            LastHash: previous?.Hash,
            EventsChecked: checkedLinks,
            BrokenAtSequence: broken?.Sequence,
            Problem: problem,
            UnsealedEvents: unsealed,
            CheckpointMatches: checkpointMatches);
    }

    private static (long Sequence, string Problem)? Check(AuditChainLink? previous, AuditChainLink link, AuditEvent? auditEvent)
    {
        if (previous is not null && link.Sequence != previous.Sequence + 1)
        {
            return (previous.Sequence + 1, $"The links {previous.Sequence + 1} to {link.Sequence - 1} are missing.");
        }

        if (previous is not null && link.PreviousHash != previous.Hash)
        {
            return (link.Sequence, $"Link {link.Sequence} does not continue the hash of link {previous.Sequence}.");
        }

        if (auditEvent is null)
        {
            return (link.Sequence, $"The event of link {link.Sequence} was deleted.");
        }

        if (AuditChainHash.Compute(link.Sequence, link.PreviousHash, auditEvent) != link.Hash)
        {
            return (link.Sequence, $"The event or the hash of link {link.Sequence} was changed.");
        }

        return null;
    }

    private async Task<bool?> MatchesCheckpointAsync(
        long sequence,
        string hash,
        AuditChainLink? first,
        AuditChainLink? last,
        CancellationToken cancellationToken)
    {
        // Deleted after the retention: nothing to compare with.
        if (first is not null && sequence < first.Sequence)
        {
            return null;
        }

        // A checkpoint beyond the end means the end of the chain was cut off.
        if (last is null || sequence > last.Sequence)
        {
            return false;
        }

        var link = await auditChainRepository.GetLink(sequence, cancellationToken);

        return string.Equals(link?.Hash, hash, StringComparison.OrdinalIgnoreCase);
    }
}
