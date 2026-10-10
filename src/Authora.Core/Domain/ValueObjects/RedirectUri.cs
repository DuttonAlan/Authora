// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.ValueObjects;

/// <summary>
///     A Redirect URI registered for a Client, to which the authorization result is sent.
///     Immutable and compared by value.
/// </summary>
/// <remarks>
///     Instances can only be created via <see cref="Create(string)"/>, so an invalid Redirect URI can never exist.
/// </remarks>
public sealed record RedirectUri
{
    /// <summary>
    ///     The Redirect URI exactly as it was registered.
    /// </summary>
    public string Value { get; }

    private RedirectUri(string value) => Value = value;

    /// <summary>
    ///     Creates a validated Redirect URI.
    /// </summary>
    /// <param name="value">The Redirect URI to register.</param>
    /// <returns>The created Redirect URI.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when the URI is not absolute, contains a fragment, contains whitespaces, or does not use HTTPS
    ///     (HTTP is only allowed for loopback addresses).
    /// </exception>
    public static RedirectUri Create(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Redirect URI must be absolute.", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Redirect URI must not contain a fragment.", nameof(value));
        }

        var isLoopback = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !isLoopback)
        {
            throw new ArgumentException("Redirect URI must use HTTPS (HTTP only for loopback).", nameof(value));
        }

        if (value != value.Trim())
        {
            throw new ArgumentException("Redirect URI can't contain whitespaces.", nameof(value));
        }

        return new RedirectUri(value);
    }

    /// <summary>
    ///     Checks whether the requested Redirect URI exactly matches this one (case-sensitive), as required by OAuth 2.1.
    /// </summary>
    /// <param name="requested">The <c>redirect_uri</c> from the authorization request.</param>
    /// <returns><see langword="true"/> if both URIs are identical, otherwise <see langword="false"/>.</returns>
    public bool Matches(string requested) => string.Equals(Value, requested, StringComparison.Ordinal);
}
