// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Hashes and verifies high-entropy secrets created by <see cref="ISecretGenerator"/>.
/// </summary>
/// <remarks>
///     Not suitable for passwords, which need a slow hash.
/// </remarks>
public interface ISecretHasher
{
    /// <summary>
    ///     Computes the one-way hash of a secret.
    /// </summary>
    /// <param name="secret">The plain-text secret.</param>
    /// <returns>The hash to store.</returns>
    public string Hash(string secret);

    /// <summary>
    ///     Checks in constant time whether a secret matches a stored hash.
    /// </summary>
    /// <param name="secret">The plain-text secret to check.</param>
    /// <param name="hash">The stored hash.</param>
    /// <returns>True if the secret matches the hash.</returns>
    public bool Verify(string secret, string hash);
}
