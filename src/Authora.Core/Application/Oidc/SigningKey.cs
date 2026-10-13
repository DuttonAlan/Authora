// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     An RSA key that signs ID Tokens with <see cref="Algorithm"/>, or that is only published for verification.
/// </summary>
/// <remarks>
///     The private key never leaves this class: only <see cref="Sign(byte[])"/> uses it, and
///     <see cref="ExportPublicParameters"/> exports the public part.
/// </remarks>
public sealed class SigningKey : IDisposable
{
    /// <summary>
    ///     The JWS algorithm of all keys (RSASSA-PKCS1-v1_5 with SHA-256). Every OpenID Provider must support it.
    /// </summary>
    public const string Algorithm = "RS256";

    /// <summary>
    ///     Smallest allowed key size in bits.
    /// </summary>
    public const int MinKeySize = 2048;

    private readonly RSA _rsa;

    // RSA instances are not documented as thread-safe, and the key is shared by all requests.
    private readonly Lock _lock = new();

    /// <summary>
    ///     Creates a key from an RSA key. The key is owned by the new instance from then on.
    /// </summary>
    /// <param name="rsa">The RSA key, with or without the private part.</param>
    /// <exception cref="ArgumentException">Thrown when the key is shorter than <see cref="MinKeySize"/>.</exception>
    public SigningKey(RSA rsa)
    {
        ArgumentNullException.ThrowIfNull(rsa);

        if (rsa.KeySize < MinKeySize)
        {
            throw new ArgumentException($"The RSA key must have at least {MinKeySize} bits, but has {rsa.KeySize}.", nameof(rsa));
        }

        _rsa = rsa;
        CanSign = HasPrivateKey(rsa);
        KeyId = ComputeThumbprint(rsa.ExportParameters(includePrivateParameters: false));
    }

    /// <summary>
    ///     The <c>kid</c> of the key: its JWK Thumbprint (RFC 7638). It is derived from the public key, so it is unique
    ///     per key and stays the same across restarts and instances.
    /// </summary>
    public string KeyId { get; }

    /// <summary>
    ///     Whether the key has a private part and can sign.
    /// </summary>
    public bool CanSign { get; }

    /// <summary>
    ///     Exports the public part of the key.
    /// </summary>
    /// <returns>The modulus and exponent. The private parameters are never set.</returns>
    public RSAParameters ExportPublicParameters()
    {
        lock (_lock)
        {
            return _rsa.ExportParameters(includePrivateParameters: false);
        }
    }

    /// <summary>
    ///     Signs data with <see cref="Algorithm"/>.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <returns>The signature.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key has no private part.</exception>
    public byte[] Sign(byte[] data)
    {
        if (!CanSign)
        {
            throw new InvalidOperationException($"The key {KeyId} has no private part and cannot sign.");
        }

        lock (_lock)
        {
            return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    /// <summary>
    ///     Disposes the RSA key.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _rsa.Dispose();
        }
    }

    private static bool HasPrivateKey(RSA rsa)
    {
        try
        {
            rsa.ExportParameters(includePrivateParameters: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // RFC 7638, Section 3.2: the required members in lexicographic order, without whitespace.
    private static string ComputeThumbprint(RSAParameters parameters)
    {
        var json = $"{{\"e\":\"{Base64Url.EncodeToString(parameters.Exponent)}\",\"kty\":\"RSA\",\"n\":\"{Base64Url.EncodeToString(parameters.Modulus)}\"}}";

        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
