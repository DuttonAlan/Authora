// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     Creates signed JSON Web Tokens in the JWS Compact Serialization (RFC 7519, RFC 7515).
/// </summary>
internal static class JsonWebToken
{
    /// <summary>
    ///     Creates a JWT signed with <see cref="SigningKey.Algorithm"/>.
    /// </summary>
    /// <param name="claims">The claims of the payload, with string, number or boolean values.</param>
    /// <param name="key">The key that signs the token. Its <c>kid</c> is put into the header.</param>
    /// <returns>The JWT: <c>header.payload.signature</c>, each Base64Url encoded.</returns>
    public static string Create(IReadOnlyDictionary<string, object> claims, SigningKey key)
    {
        var header = new Dictionary<string, string>
        {
            ["alg"] = SigningKey.Algorithm,
            ["kid"] = key.KeyId,
            ["typ"] = "JWT"
        };

        var signingInput = $"{Encode(header)}.{Encode(claims)}";
        var signature = key.Sign(Encoding.ASCII.GetBytes(signingInput));

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private static string Encode<T>(T value)
    {
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));
    }
}
