// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     Thrown when a request to an external identity provider fails or its response is not accepted, e.g. an ID Token
///     with a wrong signature. The message is a reason for the logs and the audit log; it never contains tokens or
///     secrets.
/// </summary>
public sealed class ExternalSignInException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalSignInException"/> class.
    /// </summary>
    public ExternalSignInException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalSignInException"/> class with a reason.
    /// </summary>
    /// <param name="message">The reason, without tokens or secrets.</param>
    public ExternalSignInException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalSignInException"/> class with a reason and the cause.
    /// </summary>
    /// <param name="message">The reason, without tokens or secrets.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public ExternalSignInException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
