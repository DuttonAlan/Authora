// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Exceptions;

/// <summary>
///     Thrown when the caller may not perform a change, e.g. because it would give them or another User more rights than
///     they hold. The API returns 403 Forbidden.
/// </summary>
public sealed class ForbiddenException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    public ForbiddenException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ForbiddenException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ForbiddenException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ForbiddenException"/> class with a message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
