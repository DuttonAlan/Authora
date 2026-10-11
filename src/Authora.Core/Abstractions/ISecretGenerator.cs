// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Generates cryptographically random secrets, e.g. Client Secrets.
/// </summary>
public interface ISecretGenerator
{
    /// <summary>
    ///     Generates a new random secret with at least 256 bits of entropy, encoded as URL-safe text.
    /// </summary>
    /// <returns>The plain-text secret.</returns>
    public string Generate();
}
