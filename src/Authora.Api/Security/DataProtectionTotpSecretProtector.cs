// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.Security;

/// <summary>
///     Encrypts the secrets of authenticator apps with ASP.NET Core Data Protection, under a purpose of its own, so
///     no other protected value (e.g. an admin interface cookie) can be passed off as one.
/// </summary>
/// <remarks>
///     The secrets are stored for as long as the User keeps the app, much longer than any cookie. Data Protection keeps
///     retired keys to decrypt them, so the whole key ring (<c>Authora:DataProtection:KeysPath</c>, and the certificate
///     that encrypts it) must be kept and backed up; deleting keys makes every stored secret unreadable.
/// </remarks>
/// <param name="dataProtectionProvider">The Data Protection of Authora.</param>
public sealed class DataProtectionTotpSecretProtector(IDataProtectionProvider dataProtectionProvider) : ITotpSecretProtector
{
    /// <summary>
    ///     The purpose of the protector. Changing it makes every stored secret unreadable.
    /// </summary>
    public const string Purpose = "Authora.Mfa.TotpSecret.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    /// <inheritdoc/>
    public string Protect(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Convert.ToBase64String(_protector.Protect(secret));
    }

    /// <inheritdoc/>
    public byte[]? Unprotect(string protectedSecret)
    {
        ArgumentNullException.ThrowIfNull(protectedSecret);

        try
        {
            return _protector.Unprotect(Convert.FromBase64String(protectedSecret));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            // The key is missing or revoked, or the value was changed.
            return null;
        }
    }
}
