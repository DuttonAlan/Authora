// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Authora.Core.Domain.Exceptions;

/// <summary>
///     Thrown when a value violates a validation rule of Authora, e.g. a Name that is too long.
///     The API returns 400 Bad Request with the message.
/// </summary>
/// <remarks>
///     Only this exception is returned as 400. A plain <see cref="ArgumentException"/> is thrown by framework code
///     (e.g. EF Core) for bugs, which must stay a 500 without exposing the message.
/// </remarks>
public sealed class DomainValidationException : ArgumentException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainValidationException"/> class.
    /// </summary>
    public DomainValidationException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainValidationException"/> class with a message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DomainValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainValidationException"/> class with a message and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DomainValidationException"/> class with a message and the
    ///     name of the invalid parameter.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="paramName">The name of the invalid parameter.</param>
    public DomainValidationException(string message, string? paramName)
        : base(message, paramName)
    {
    }

    /// <summary>
    ///     Throws when a value is <see langword="null"/>, empty or only whitespace.
    /// </summary>
    /// <remarks>
    ///     Hides <see cref="ArgumentException.ThrowIfNullOrWhiteSpace(string?, string?)"/> on purpose, so invalid input
    ///     results in 400 Bad Request instead of 500.
    /// </remarks>
    /// <param name="argument">The value to check.</param>
    /// <param name="paramName">The name of the parameter. Filled in by the compiler.</param>
    /// <exception cref="DomainValidationException">Thrown when the value is null, empty or only whitespace.</exception>
    public static new void ThrowIfNullOrWhiteSpace(
        [NotNull] string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new DomainValidationException("The value must not be empty.", paramName);
        }
    }
}
