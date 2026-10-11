// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Settings of the OAuth authorization server, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class OAuthOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:OAuth";

    /// <summary>
    ///     The Issuer Identifier of Authora, e.g. <c>https://auth.example.com</c>. Returned as <c>iss</c> in
    ///     authorization responses (RFC 9207) and in introspection responses. Must use HTTPS, except for loopback
    ///     hosts during development, and must not contain a query or fragment.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    ///     Lifetime of Authorization Codes. Defaults to 60 seconds, at most
    ///     <see cref="AuthorizationCode.MaxLifetime"/>.
    /// </summary>
    public TimeSpan AuthorizationCodeLifetime { get; set; } = TimeSpan.FromSeconds(60);
}
