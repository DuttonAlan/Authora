// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Oidc;

namespace Authora.Core.Abstractions;

/// <summary>
///     Provides the keys that sign ID Tokens and that are published in the JWKS (US-024).
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>
    ///     The key that signs new ID Tokens. Always has a private part.
    /// </summary>
    public SigningKey SigningKey { get; }

    /// <summary>
    ///     All keys Clients may use to verify ID Tokens: <see cref="SigningKey"/> first, then the keys that are only
    ///     published for key rotation.
    /// </summary>
    public IReadOnlyList<SigningKey> PublishedKeys { get; }
}
