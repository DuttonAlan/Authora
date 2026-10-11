// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Hashes and verifies User passwords with a slow, salted hash.
/// </summary>
/// <remarks>
///     Not to be confused with <see cref="ISecretHasher"/>, which is only suitable for high-entropy random secrets.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    ///     Computes the salted hash of a password.
    /// </summary>
    /// <param name="password">The plain-text password.</param>
    /// <returns>The encoded hash, including its algorithm parameters and salt.</returns>
    public string Hash(string password);

    /// <summary>
    ///     Checks whether a password matches a stored hash.
    /// </summary>
    /// <param name="passwordHash">The stored hash.</param>
    /// <param name="password">The plain-text password to check.</param>
    /// <returns>The result of the verification.</returns>
    public PasswordVerification Verify(string passwordHash, string password);
}
