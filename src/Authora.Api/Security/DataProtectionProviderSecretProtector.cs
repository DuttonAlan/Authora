// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.Security;

/// <summary>
///     Encrypts the Client Secrets of external identity providers with ASP.NET Core Data Protection, under a purpose of
///     its own, so no other protected value (e.g. a cookie) can be passed off as one.
/// </summary>
/// <remarks>
///     Like the secrets of authenticator apps, they are stored for as long as the provider exists. Data Protection keeps
///     retired keys to decrypt them, so the whole key ring must be kept and backed up; without it, every provider needs
///     its secret set again.
/// </remarks>
/// <param name="dataProtectionProvider">The Data Protection of Authora.</param>
public sealed class DataProtectionProviderSecretProtector(IDataProtectionProvider dataProtectionProvider) : IProviderSecretProtector
{
    /// <summary>
    ///     The purpose of the protector. Changing it makes every stored secret unreadable.
    /// </summary>
    public const string Purpose = "Authora.IdentityProviders.ClientSecret.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    /// <inheritdoc/>
    public string Protect(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return _protector.Protect(secret);
    }

    /// <inheritdoc/>
    public string? Unprotect(string protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);

        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            // The key is missing or revoked, or the value was changed.
            return null;
        }
    }
}
