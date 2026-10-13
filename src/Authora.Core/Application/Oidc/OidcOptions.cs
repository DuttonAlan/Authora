// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Oidc;

/// <summary>
///     Settings of OpenID Connect, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class OidcOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Oidc";

    /// <summary>
    ///     Longest allowed <see cref="IdTokenLifetime"/>.
    /// </summary>
    public static readonly TimeSpan MaxIdTokenLifetime = TimeSpan.FromHours(1);

    /// <summary>
    ///     Path of a PEM file with the RSA private key (at least 2048 bits) that signs ID Tokens. Relative paths are
    ///     resolved against the content root. Required. The key is kept out of the database, so a copy of the
    ///     database cannot be used to forge ID Tokens.
    /// </summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>
    ///     Paths of PEM files with further RSA keys that are published in the JWKS but never sign, for key rotation:
    ///     the next key before it signs, and the previous key until the ID Tokens it signed have expired.
    ///     Public keys are enough, of private keys only the public part is published.
    /// </summary>
    public List<string> AdditionalKeyPaths { get; set; } = [];

    /// <summary>
    ///     Lifetime of ID Tokens. Defaults to 5 minutes, at most <see cref="MaxIdTokenLifetime"/>. An ID Token is only
    ///     meant to be validated right after sign-in, not kept as a session.
    /// </summary>
    public TimeSpan IdTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);
}
