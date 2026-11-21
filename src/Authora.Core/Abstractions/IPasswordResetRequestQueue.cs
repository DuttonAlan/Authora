// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.RequestPasswordReset;

namespace Authora.Core.Abstractions;

/// <summary>
///     Hands requests for a password reset link to a background service, which runs
///     <see cref="ProcessPasswordResetRequestService"/> for each.
/// </summary>
/// <remarks>
///     The page only queues the address, so its response time does not depend on whether an account has it: looking
///     up the address, issuing the token and sending the email all happen later. Requests still queued when the server
///     stops are lost.
/// </remarks>
public interface IPasswordResetRequestQueue
{
    /// <summary>
    ///     Queues a request.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <returns><see langword="true"/> if the request was queued, <see langword="false"/> if the queue is full.</returns>
    public bool TryEnqueue(QueuedPasswordResetRequest request);
}
