// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit;

namespace Authora.Core.Abstractions;

/// <summary>
///     Records security-relevant events in the audit log, with the actor of the current request.
/// </summary>
/// <remarks>
///     Never pass passwords, tokens, codes or Secrets, not even in the Details.
/// </remarks>
public interface IAuditLog
{
    /// <summary>
    ///     Adds an event that is saved together with the next save of the repositories of this request, so it is
    ///     stored if and only if the change it describes is stored. Call it right before saving the change.
    /// </summary>
    /// <param name="entry">The event.</param>
    public void Record(AuditEntry entry);

    /// <summary>
    ///     Saves an event right away, independent of other changes of this request. For failed attempts and events
    ///     that are not part of a change, e.g. a failed sign-in.
    /// </summary>
    /// <param name="entry">The event.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the event is saved.</returns>
    public Task RecordNowAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
