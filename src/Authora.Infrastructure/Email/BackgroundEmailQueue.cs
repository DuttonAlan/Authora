// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Threading.Channels;

using Authora.Core.Abstractions;
using Authora.Core.Application.Email;

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Keeps emails in memory until <see cref="BackgroundEmailDeliveryService"/> sends them.
/// </summary>
/// <param name="logger">The Logger to record a full queue.</param>
public sealed class BackgroundEmailQueue(ILogger<BackgroundEmailQueue> logger) : IEmailQueue
{
    /// <summary>
    ///     How many emails the queue holds at most. Further emails are dropped until the delivery catches up.
    /// </summary>
    public const int Capacity = 1_000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    /// <summary>
    ///     The queued emails, read by the delivery.
    /// </summary>
    internal ChannelReader<EmailMessage> Reader => _channel.Reader;

    /// <inheritdoc/>
    public bool TryEnqueue(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // With FullMode.Wait, TryWrite returns false instead of waiting when the queue is full.
        if (_channel.Writer.TryWrite(message))
        {
            return true;
        }

        logger.EmailQueueFull(Capacity, message.Subject);

        return false;
    }
}
