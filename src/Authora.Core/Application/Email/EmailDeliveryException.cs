// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Email;

/// <summary>
///     Thrown when an email could not be handed to the mail server, e.g. because it is unreachable or rejected the
///     sender or recipient.
/// </summary>
/// <remarks>
///     The message contains no addresses and can be logged. The <see cref="Exception.InnerException"/> can quote the
///     server's reply, which may contain addresses, so it must not be logged.
/// </remarks>
public sealed class EmailDeliveryException : Exception
{
    /// <summary>
    ///     Creates the exception.
    /// </summary>
    public EmailDeliveryException()
    {
    }

    /// <summary>
    ///     Creates the exception with a message.
    /// </summary>
    /// <param name="message">What went wrong, without the content of the email.</param>
    public EmailDeliveryException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates the exception with a message and the cause.
    /// </summary>
    /// <param name="message">What went wrong, without the content of the email.</param>
    /// <param name="innerException">The cause.</param>
    public EmailDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
