// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;

namespace Authora.Core.Abstractions;

/// <summary>
///     Sends emails in the background through <see cref="IEmailSender"/>, so a request does not wait for the mail
///     server.
/// </summary>
/// <remarks>
///     Used where the response time must not reveal whether an email was sent, e.g. whether an account exists for an
///     address. Delivery failures are only logged. Emails still queued when the server stops are lost.
/// </remarks>
public interface IEmailQueue
{
    /// <summary>
    ///     Queues an email for delivery.
    /// </summary>
    /// <param name="message">The email to send.</param>
    /// <returns><see langword="true"/> if the email was queued, <see langword="false"/> if the queue is full.</returns>
    public bool TryEnqueue(EmailMessage message);
}
