// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

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
    /// <exception cref="DomainValidationException">
    ///     Thrown when the URI is not absolute, contains a fragment, user information or whitespaces, or does not use
    ///     HTTPS (HTTP is only allowed for the loopback IP literals <c>127.0.0.1</c> and <c>[::1]</c>).
    /// </exception>
    public static RedirectUri Create(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new DomainValidationException("Redirect URI must be absolute.", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            throw new DomainValidationException("Redirect URI must not contain a fragment.", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DomainValidationException("Redirect URI must not contain user information.", nameof(value));
        }

        // RFC 8252, Section 8.3: loopback redirects must use an IP literal, because "localhost" depends on name
        // resolution and could be resolved to a remote host.
        var isLoopbackIp = uri.Scheme == Uri.UriSchemeHttp
            && uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            && uri.IsLoopback;

        if (uri.Scheme != Uri.UriSchemeHttps && !isLoopbackIp)
        {
            throw new DomainValidationException(
                "Redirect URI must use HTTPS (HTTP only for the loopback IP addresses 127.0.0.1 and [::1]).",
                nameof(value));
        }

        if (value != value.Trim())
        {
            throw new DomainValidationException("Redirect URI can't contain whitespaces.", nameof(value));
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
