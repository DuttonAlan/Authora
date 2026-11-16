// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     Creates and reads signed JSON Web Tokens in the JWS Compact Serialization (RFC 7519, RFC 7515).
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

    /// <summary>
    ///     Reads the claims of a JWT that one of the given keys signed with <see cref="SigningKey.Algorithm"/>. Only
    ///     checks the header and the signature, not the claims, e.g. not <c>exp</c>.
    /// </summary>
    /// <param name="token">The JWT in the JWS Compact Serialization.</param>
    /// <param name="keys">The keys that may have signed it, found by the <c>kid</c> of the header.</param>
    /// <param name="claims">The claims of the payload, if the token is valid.</param>
    /// <returns>True if the token is well-formed, uses <see cref="SigningKey.Algorithm"/> and its signature is valid.</returns>
    public static bool TryReadSigned(string token, IReadOnlyList<SigningKey> keys, out JsonElement claims)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(keys);

        claims = default;

        var parts = token.Split('.');

        if (parts.Length != 3 || !Base64Url.IsValid(parts[0]) || !Base64Url.IsValid(parts[1]) || !Base64Url.IsValid(parts[2]))
        {
            return false;
        }

        try
        {
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));

            // Only the algorithm Authora signs with is accepted, so "none" or HS256 with the public key cannot be used.
            if (header.RootElement.ValueKind != JsonValueKind.Object
                || !header.RootElement.TryGetProperty("alg", out var alg)
                || alg.ValueKind != JsonValueKind.String
                || alg.GetString() != SigningKey.Algorithm
                || !header.RootElement.TryGetProperty("kid", out var kid)
                || kid.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var key = keys.FirstOrDefault(key => key.KeyId == kid.GetString());

            if (key is null
                || !key.Verify(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2])))
            {
                return false;
            }

            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));

            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            claims = payload.RootElement.Clone();

            return true;
        }
        catch (Exception exception) when (exception is JsonException or System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static string Encode<T>(T value)
    {
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));
    }
}
