// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.RequestPasswordReset;

/// <summary>
///     The Service for a User who forgot their password to ask for a reset link by email.
/// </summary>
/// <remarks>
///     The caller learns nothing about whether an account exists: the result is the same for every address, and the
///     request is only queued, without touching the database. <see cref="ProcessPasswordResetRequestService"/> looks
///     up the address and sends the link in the background, so the response time does not depend on the account or
///     the mail server.
/// </remarks>
/// <param name="emailSender">Tells whether email delivery is configured.</param>
/// <param name="queue">Hands the request to the background processing.</param>
/// <param name="actorAccessor">Provides the actor of the request, for the audit events recorded later.</param>
/// <param name="options">The password reset settings.</param>
public sealed class RequestPasswordResetService(
    IEmailSender emailSender,
    IPasswordResetRequestQueue queue,
    IAuditActorAccessor actorAccessor,
    IOptions<PasswordResetOptions> options)
{
    /// <summary>
    ///     Whether Users can ask for a reset link: email delivery is configured and
    ///     <see cref="PasswordResetOptions.SelfServiceEnabled"/> is set.
    /// </summary>
    public bool IsAvailable => emailSender.IsEnabled && options.Value.SelfServiceEnabled;

    /// <summary>
    ///     Queues the request. A link is sent later if an account that can sign in has the address.
    /// </summary>
    /// <remarks>
    ///     A request is dropped while the queue is full; the queue logs that. Nothing is stored or counted for it.
    /// </remarks>
    /// <param name="request">The request with the address.</param>
    /// <exception cref="InvalidOperationException">Thrown when the reset is not <see cref="IsAvailable"/>. The caller checks this first.</exception>
    public void Execute(RequestPasswordResetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAvailable)
        {
            throw new InvalidOperationException("The password reset by email is not available.");
        }

        _ = queue.TryEnqueue(new QueuedPasswordResetRequest(request.Email, actorAccessor.Current));
    }
}
