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

    /// <summary>
    ///     Waits until a number of emails were sent to an address, e.g. by the background delivery.
    /// </summary>
    /// <param name="address">The address of the recipient.</param>
    /// <param name="count">How many emails to wait for.</param>
    /// <returns>The emails sent to the address, oldest first.</returns>
    /// <exception cref="TimeoutException">Thrown when fewer emails arrived within ten seconds.</exception>
    public async Task<IReadOnlyList<EmailMessage>> WaitForAsync(string address, int count = 1)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (SentTo(address) is var sent && sent.Count < count)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Expected {count} emails to {address}, got {sent.Count}.");
            }
        }

        return SentTo(address);
    }

    /// <summary>
    ///     When set, <see cref="SendAsync"/> fails like an unreachable mail server.
    /// </summary>
    public bool Fail { get; set; }

    /// <inheritdoc/>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled)
        {
            throw new InvalidOperationException("Email delivery is not configured.");
        }

        if (Fail)
        {
            throw new EmailDeliveryException("The email could not be sent through smtp.example.com:587: connection refused.");
        }

        _sent.Enqueue(message);

        return Task.CompletedTask;
    }
}
