// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Abstractions;

/// <summary>
///     Encrypts the Client Secrets Authora was given by external identity providers. Unlike Authora's own secrets they
///     cannot be hashed: Authora must send them to the provider.
/// </summary>
/// <remarks>
///     Implemented with ASP.NET Core Data Protection under a purpose of its own, like <see cref="ITotpSecretProtector"/>,
///     so the key ring is part of every backup: without it, no provider can be used until its secret is set again.
/// </remarks>
public interface IProviderSecretProtector
{
    /// <summary>
    ///     Encrypts a Client Secret.
    /// </summary>
    /// <param name="secret">The secret in plain text.</param>
    /// <returns>The encrypted secret as text.</returns>
    public string Protect(string secret);

    /// <summary>
    ///     Decrypts a Client Secret.
    /// </summary>
    /// <param name="protectedSecret">The encrypted secret.</param>
    /// <returns>The secret in plain text, or null if it cannot be decrypted, e.g. because the key ring was lost.</returns>
    public string? Unprotect(string protectedSecret);
}
