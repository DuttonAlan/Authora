// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Authora.Core.Abstractions;
using Authora.Core.Application.Email;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;
using MimeKit.Utils;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Sends emails through the SMTP server of the <see cref="EmailOptions"/>, with a new connection per email.
/// </summary>
/// <param name="options">The SMTP server, credentials and sender.</param>
/// <param name="environment">The host environment, whose content root relative paths are resolved against.</param>
/// <param name="logger">The Logger to record sent and failed emails.</param>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    IHostEnvironment environment,
    ILogger<SmtpEmailSender> logger) : IEmailSender, IDisposable
{
    private readonly Lazy<X509Certificate2?> _trustedCertificate = new(() =>
        options.Value.TrustedCertificatePath is { } path ? SmtpCertificateTrust.Load(path, environment.ContentRootPath) : null);

    /// <inheritdoc/>
    public bool IsEnabled => options.Value.IsEnabled;

    /// <inheritdoc/>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;

        // The validator requires the sender address whenever the host is set.
        if (!settings.IsEnabled || settings.Host is not { } host || settings.SenderAddress is not { } senderAddress)
        {
            throw new InvalidOperationException(
                $"Email delivery is not configured: '{EmailOptions.SectionName}:{nameof(EmailOptions.Host)}' is not set.");
        }

        var port = settings.EffectivePort;

        // With the sender's domain instead of the default, which reveals the host name of the server.
        var messageId = MimeUtils.GenerateMessageId(senderAddress[(senderAddress.LastIndexOf('@') + 1)..]);

        using var mimeMessage = CreateMimeMessage(message, settings.SenderName, senderAddress, messageId);
        using var client = new SmtpClient();

        client.Timeout = (int)settings.Timeout.TotalMilliseconds;

        if (_trustedCertificate.Value is { } trusted)
        {
            client.ServerCertificateValidationCallback = (_, certificate, chain, errors) =>
                SmtpCertificateTrust.IsTrusted(trusted, certificate, chain, errors);
        }

        // Covers connecting too, which the Timeout of the client does not.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);

        try
        {
            await client.ConnectAsync(host, port, ToSocketOptions(settings.TlsMode), timeout.Token);

            if (settings.Username is { Length: > 0 } username)
            {
                await client.AuthenticateAsync(username, settings.Password ?? string.Empty, timeout.Token);
            }

            await client.SendAsync(mimeMessage, timeout.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Also a timeout, which cancels the linked token. Only the reason is logged and put into the message:
            // the exception can contain the server's reply, which may quote the sender or recipient address.
            var reason = DescribeFailure(exception);

            logger.EmailFailed(messageId, host, port, reason);

            throw new EmailDeliveryException($"The email could not be sent through {host}:{port}: {reason}.", exception);
        }

        // The server has accepted the email, so a failure from here on must not make the caller send it again.
        logger.EmailSent(messageId);

        try
        {
            await client.DisconnectAsync(quit: true, timeout.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.EmailDisconnectFailed(messageId, DescribeFailure(exception));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_trustedCertificate.IsValueCreated)
        {
            _trustedCertificate.Value?.Dispose();
        }
    }

    private static MimeMessage CreateMimeMessage(EmailMessage message, string senderName, string senderAddress, string messageId)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(senderName, senderAddress));
        mimeMessage.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToAddress));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Date = DateTimeOffset.UtcNow;
        mimeMessage.MessageId = messageId;

        mimeMessage.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        return mimeMessage;
    }

    /// <summary>
    ///     Describes a failure without the text of the server's reply or of the exception, which may contain addresses.
    /// </summary>
    internal static string DescribeFailure(Exception exception) => exception switch
    {
        SmtpCommandException command => string.Create(
            CultureInfo.InvariantCulture,
            $"the server rejected the command ({command.ErrorCode}, status {(int)command.StatusCode})"),
        AuthenticationException => "authentication at the server failed",
        SslHandshakeException => "the TLS handshake failed, e.g. because the server's certificate is not trusted",
        SmtpProtocolException => "the server violated the SMTP protocol",
        NotSupportedException => "the server does not support the required TLS mode or authentication",
        OperationCanceledException or TimeoutException => "the timeout passed",
        SocketException socket => $"the connection failed ({socket.SocketErrorCode})",
        IOException { InnerException: SocketException socket } => $"the connection failed ({socket.SocketErrorCode})",
        IOException => "the connection was interrupted",
        _ => $"unexpected {exception.GetType().Name}"
    };

    private static SecureSocketOptions ToSocketOptions(SmtpTlsMode tlsMode) => tlsMode switch
    {
        SmtpTlsMode.StartTls => SecureSocketOptions.StartTls,
        SmtpTlsMode.Implicit => SecureSocketOptions.SslOnConnect,
        SmtpTlsMode.None => SecureSocketOptions.None,
        _ => throw new ArgumentOutOfRangeException(nameof(tlsMode), tlsMode, "Unknown TLS mode.")
    };
}
