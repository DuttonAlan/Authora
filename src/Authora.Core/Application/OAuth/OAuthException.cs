// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Thrown by the OAuth endpoints when a request must be answered with an OAuth error response,
///     e.g. <c>{"error": "invalid_grant"}</c>.
/// </summary>
/// <remarks>
///     The <see cref="Exception.Message"/> is sent to the Client as <c>error_description</c>, so it must not contain
///     internal details, and only the characters RFC 6749 allows there (printable ASCII without <c>"</c> and <c>\</c>).
/// </remarks>
public sealed class OAuthException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthException"/> class.
    /// </summary>
    public OAuthException()
        : this(OAuthErrors.InvalidRequest, "The request is invalid.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthException"/> class with the
    ///     <see cref="OAuthErrors.InvalidRequest"/> error and a description.
    /// </summary>
    /// <param name="message">The description of the error, sent as <c>error_description</c>.</param>
    public OAuthException(string message)
        : this(OAuthErrors.InvalidRequest, message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthException"/> class with the
    ///     <see cref="OAuthErrors.InvalidRequest"/> error, a description and an inner exception.
    /// </summary>
    /// <param name="message">The description of the error, sent as <c>error_description</c>.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public OAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = OAuthErrors.InvalidRequest;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthException"/> class with an error code and a description.
    /// </summary>
    /// <param name="error">The error code, see <see cref="OAuthErrors"/>.</param>
    /// <param name="description">The description of the error, sent as <c>error_description</c>.</param>
    public OAuthException(string error, string description)
        : base(description)
    {
        Error = error;
    }

    /// <summary>
    ///     The error code, see <see cref="OAuthErrors"/>.
    /// </summary>
    public string Error { get; }
}
