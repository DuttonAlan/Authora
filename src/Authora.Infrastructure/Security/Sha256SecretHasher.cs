// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Authora.Core.Abstractions;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Hashes secrets with SHA-256, encoded as Base64Url.
/// </summary>
/// <remarks>
///     A fast hash is sufficient because the secrets are 256-bit random values that cannot be brute-forced,
///     and it keeps the /token endpoint fast. Never use it for passwords.
/// </remarks>
public sealed class Sha256SecretHasher : ISecretHasher
{
    /// <inheritdoc/>
    public SecretHash Hash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return SecretHash.FromHashedValue(Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));
    }

    /// <inheritdoc/>
    public bool Verify(string secret, SecretHash hash)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(hash);

        if (!Base64Url.IsValid(hash.Value, out var decodedLength) || decodedLength != SHA256.HashSizeInBytes)
        {
            return false;
        }

        var expected = Base64Url.DecodeFromChars(hash.Value);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
