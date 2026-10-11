// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Exceptions;

/// <summary>
///     Thrown when a requested resource does not exist. The API returns 404 Not Found.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    public NotFoundException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotFoundException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotFoundException"/> class with a message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
