// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Collections.Concurrent;

using Authora.Core.Abstractions;
using Authora.Core.Application.Email;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Replaces the SMTP delivery in the tests: keeps the sent emails, so a test can read the links in them.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     The sent emails, oldest first.
    /// </summary>
    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    /// <summary>
    ///     The emails sent to an address, oldest first.
    /// </summary>
    /// <param name="address">The address of the recipient.</param>
    /// <returns>The emails.</returns>
    public IReadOnlyList<EmailMessage> SentTo(string address)
    {
        return [.. _sent.Where(message => string.Equals(message.ToAddress, address, StringComparison.OrdinalIgnoreCase))];
    }

    /// <inheritdoc/>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled)
        {
            throw new InvalidOperationException("Email delivery is not configured.");
        }

        _sent.Enqueue(message);

        return Task.CompletedTask;
    }
}
