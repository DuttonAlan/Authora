// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Text.Json.Serialization;

namespace Authora.Core.Application.Oidc.Jwks;

/// <summary>
///     The JSON Web Key Set of the JWKS endpoint (RFC 7517, Section 5).
/// </summary>
/// <param name="Keys">The public keys.</param>
public sealed record JsonWebKeySet([property: JsonPropertyName("keys")] IReadOnlyList<JsonWebKey> Keys)
{
    /// <summary>
    ///     Creates the set of the public parts of keys.
    /// </summary>
    /// <param name="keys">The keys to publish.</param>
    /// <returns>The key set.</returns>
    public static JsonWebKeySet From(IEnumerable<SigningKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return new JsonWebKeySet(keys.Select(JsonWebKey.From).ToList());
    }
}

/// <summary>
///     The public part of an RSA signing key as a JSON Web Key (RFC 7517, RFC 7518, Section 6.3.1).
/// </summary>
/// <remarks>
///     It has no members for private parameters, so a private key can never be serialized by accident.
/// </remarks>
/// <param name="KeyType">The key type, always <c>RSA</c>.</param>
/// <param name="Use">The intended use, always <c>sig</c>.</param>
/// <param name="Algorithm">The algorithm, always <see cref="SigningKey.Algorithm"/>.</param>
/// <param name="KeyId">The <c>kid</c>, see <see cref="SigningKey.KeyId"/>.</param>
/// <param name="Modulus">The modulus, Base64Url encoded.</param>
/// <param name="Exponent">The public exponent, Base64Url encoded.</param>
public sealed record JsonWebKey(
    [property: JsonPropertyName("kty")] string KeyType,
    [property: JsonPropertyName("use")] string Use,
    [property: JsonPropertyName("alg")] string Algorithm,
    [property: JsonPropertyName("kid")] string KeyId,
    [property: JsonPropertyName("n")] string Modulus,
    [property: JsonPropertyName("e")] string Exponent)
{
    /// <summary>
    ///     Creates the JSON Web Key of the public part of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The JSON Web Key.</returns>
    public static JsonWebKey From(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var parameters = key.ExportPublicParameters();

        return new JsonWebKey(
            "RSA",
            "sig",
            SigningKey.Algorithm,
            key.KeyId,
            Base64Url.EncodeToString(parameters.Modulus),
            Base64Url.EncodeToString(parameters.Exponent));
    }
}
