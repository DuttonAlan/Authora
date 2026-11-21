// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Email;

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Log messages of email delivery. Addresses, content and SMTP credentials are never logged.
/// </summary>
internal static partial class EmailLog
{
    [LoggerMessage(EventId = 14001, Level = LogLevel.Information, Message = "Email delivery is enabled through {Host}:{Port} with TLS mode {TlsMode}.")]
    public static partial void EmailEnabled(this ILogger logger, string host, int port, SmtpTlsMode tlsMode);

    [LoggerMessage(EventId = 14002, Level = LogLevel.Information, Message = "Email delivery is disabled, because 'Authora:Email:Host' is not set. Features that send email are not available.")]
    public static partial void EmailDisabled(this ILogger logger);

    [LoggerMessage(EventId = 14003, Level = LogLevel.Information, Message = "The SMTP server accepted email {MessageId}.")]
    public static partial void EmailSent(this ILogger logger, string messageId);

    // Without the exception: its message can quote the server's reply, which may contain addresses.
    [LoggerMessage(EventId = 14004, Level = LogLevel.Error, Message = "Email {MessageId} could not be sent through {Host}:{Port}: {Reason}.")]
    public static partial void EmailFailed(this ILogger logger, string messageId, string host, int port, string reason);

    [LoggerMessage(EventId = 14006, Level = LogLevel.Warning, Message = "Closing the connection after email {MessageId} failed: {Reason}. The server had already accepted the email.")]
    public static partial void EmailDisconnectFailed(this ILogger logger, string messageId, string reason);

    [LoggerMessage(EventId = 14005, Level = LogLevel.Warning, Message = "Email delivery is unencrypted (TLS mode None). Links and SMTP credentials travel in plain text.")]
    public static partial void EmailUnencrypted(this ILogger logger);

    [LoggerMessage(EventId = 14021, Level = LogLevel.Error, Message = "The email queue is full ({Capacity} emails). The email \"{Subject}\" is not sent.")]
    public static partial void EmailQueueFull(this ILogger logger, int capacity, string subject);

    // Only the message: the inner SMTP error can quote addresses, see SmtpEmailSender.
    [LoggerMessage(EventId = 14022, Level = LogLevel.Error, Message = "The queued email \"{Subject}\" could not be sent: {Reason}")]
    public static partial void QueuedEmailFailed(this ILogger logger, string subject, string reason);

    [LoggerMessage(EventId = 14023, Level = LogLevel.Warning, Message = "{Count} queued emails were not sent, because the server is stopping.")]
    public static partial void QueuedEmailsDropped(this ILogger logger, int count);
}
