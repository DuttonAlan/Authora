// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Exceptions;

/// <summary>
///     Thrown when a request conflicts with the current state, e.g. a duplicate Email.
///     The API returns 409 Conflict.
/// </summary>
public sealed class ConflictException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException"/> class.
    /// </summary>
    public ConflictException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConflictException"/> class with a message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
