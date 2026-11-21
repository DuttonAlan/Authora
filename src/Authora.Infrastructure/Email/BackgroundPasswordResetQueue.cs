// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Threading.Channels;

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.RequestPasswordReset;

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Keeps requests for a password reset link in memory until <see cref="BackgroundPasswordResetService"/> handles
///     them.
/// </summary>
/// <param name="logger">The Logger to record a full queue.</param>
public sealed class BackgroundPasswordResetQueue(ILogger<BackgroundPasswordResetQueue> logger) : IPasswordResetRequestQueue
{
    /// <summary>
    ///     How many requests the queue holds at most. Further requests are dropped, before anything is stored or
    ///     counted for them, until the processing catches up.
    /// </summary>
    public const int Capacity = 1_000;

    private readonly Channel<QueuedPasswordResetRequest> _channel = Channel.CreateBounded<QueuedPasswordResetRequest>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    /// <summary>
    ///     The queued requests, read by the processing.
    /// </summary>
    internal ChannelReader<QueuedPasswordResetRequest> Reader => _channel.Reader;

    /// <inheritdoc/>
    public bool TryEnqueue(QueuedPasswordResetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // With FullMode.Wait, TryWrite returns false instead of waiting when the queue is full.
        if (_channel.Writer.TryWrite(request))
        {
            return true;
        }

        logger.PasswordResetQueueFull(Capacity);

        return false;
    }
}
