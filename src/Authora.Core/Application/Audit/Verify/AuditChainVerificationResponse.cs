// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit.Verify;

/// <summary>
///     The result of verifying the hash chain of the audit log (TD-013).
/// </summary>
/// <param name="IsIntact" example="true">Whether no problem was found.</param>
/// <param name="FirstSequence" example="1">Sequence of the first remaining link (older ones were deleted after the retention), or null if nothing is sealed yet.</param>
/// <param name="LastSequence" example="1532">Sequence of the last link, or null if nothing is sealed yet. Compare it and LastHash with the checkpoints in the application logs.</param>
/// <param name="LastHash" example="9f2c…">Hash of the last link, or null if nothing is sealed yet.</param>
/// <param name="EventsChecked" example="1532">How many links were checked until the end or the first problem.</param>
/// <param name="BrokenAtSequence" example="null">Sequence of the first link that does not verify, or null.</param>
/// <param name="Problem" example="null">Description of the first problem, or null if the chain is intact.</param>
/// <param name="UnsealedEvents" example="0">Events that should have been sealed by now but are not part of the chain, e.g. inserted directly into the database.</param>
/// <param name="CheckpointMatches" example="true">Whether the given checkpoint matches the chain, or null if none was given or its link was deleted after the retention.</param>
public sealed record AuditChainVerificationResponse(
    bool IsIntact,
    long? FirstSequence,
    long? LastSequence,
    string? LastHash,
    long EventsChecked,
    long? BrokenAtSequence,
    string? Problem,
    int UnsealedEvents,
    bool? CheckpointMatches);
