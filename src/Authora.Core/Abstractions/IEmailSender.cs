// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;

namespace Authora.Core.Abstractions;

/// <summary>
///     Sends emails, e.g. password reset links.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    ///     Whether email delivery is configured. Features that send email must stay disabled otherwise.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    ///     Sends an email.
    /// </summary>
    /// <param name="message">The email to send.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the mail server accepted the email.</returns>
    /// <exception cref="InvalidOperationException">Thrown when email delivery is not configured, see <see cref="IsEnabled"/>.</exception>
    /// <exception cref="EmailDeliveryException">Thrown when the mail server could not be reached or rejected the email.</exception>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
