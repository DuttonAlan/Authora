// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;

using Authora.Core.Abstractions;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Generates 256-bit random secrets from a cryptographically secure source, encoded as Base64Url.
/// </summary>
public sealed class SecretGenerator : ISecretGenerator
{
    private const int SecretSizeInBytes = 32;

    /// <inheritdoc/>
    public string Generate()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretSizeInBytes));
    }
}
