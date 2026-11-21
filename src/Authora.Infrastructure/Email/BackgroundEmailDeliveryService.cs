// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Email;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Sends the emails of the <see cref="BackgroundEmailQueue"/> one after another through the
///     <see cref="IEmailSender"/>. A failed email is logged and not retried.
/// </summary>
/// <param name="queue">The queue to read.</param>
/// <param name="emailSender">Sends the emails.</param>
/// <param name="logger">The Logger to record failed emails.</param>
public sealed class BackgroundEmailDeliveryService(
    BackgroundEmailQueue queue,
    IEmailSender emailSender,
    ILogger<BackgroundEmailDeliveryService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (queue.Reader.Count > 0)
            {
                logger.QueuedEmailsDropped(queue.Reader.Count);
            }
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        try
        {
            await emailSender.SendAsync(message, stoppingToken);
        }
        catch (EmailDeliveryException exception)
        {
            logger.QueuedEmailFailed(message.Subject, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            // Email delivery is not configured. Callers check IEmailSender.IsEnabled first, so this is not expected.
            logger.QueuedEmailFailed(message.Subject, exception.Message);
        }
    }
}
