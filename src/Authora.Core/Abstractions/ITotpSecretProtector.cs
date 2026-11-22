// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Encrypts the secrets of authenticator apps for storage. Unlike passwords and tokens they cannot be hashed:
///     computing a code needs the secret itself.
/// </summary>
/// <remarks>
///     Implemented with ASP.NET Core Data Protection under a purpose of its own, so the key ring becomes part of every
///     backup: without it, no stored secret can be decrypted and every User has to fall back to a recovery code or be
///     reset by an administrator.
/// </remarks>
public interface ITotpSecretProtector
{
    /// <summary>
    ///     Encrypts a secret.
    /// </summary>
    /// <param name="secret">The secret in plain text.</param>
    /// <returns>The encrypted secret as text.</returns>
    public string Protect(byte[] secret);

    /// <summary>
    ///     Decrypts a secret.
    /// </summary>
    /// <param name="protectedSecret">The encrypted secret.</param>
    /// <returns>The secret in plain text, or null if it cannot be decrypted, e.g. because the key ring was lost.</returns>
    public byte[]? Unprotect(string protectedSecret);
}
