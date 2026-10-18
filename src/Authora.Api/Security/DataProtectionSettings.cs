// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.Security;

/// <summary>
///     Where ASP.NET Core Data Protection keeps its keys and how they are encrypted at rest (TD-015), bound from the
///     section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     The keys encrypt the antiforgery tokens and the cookies of the admin interface, which hold its Access and
///     Refresh Token (TD-014). With <see cref="CertificatePath"/>, new keys are encrypted with the certificate, so
///     reading the key directory alone no longer decrypts a stolen admin cookie.
/// </remarks>
public sealed class DataProtectionSettings
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:DataProtection";

    /// <summary>
    ///     Directory the keys are stored in, shared by all instances, e.g. a mounted volume. Without it, the keys are
    ///     kept in the home directory of the user that runs Authora, or only in memory.
    /// </summary>
    public string? KeysPath { get; set; }

    /// <summary>
    ///     Path of a PKCS#12 file (<c>.pfx</c>) with a certificate and its private key that encrypts the keys at rest.
    ///     Requires <see cref="KeysPath"/>.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    ///     Password of the file at <see cref="CertificatePath"/>, or null if it has none.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    ///     Checks the settings, including that the certificate can be loaded with its private key.
    /// </summary>
    /// <returns>The error messages, empty if the settings are valid.</returns>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(KeysPath))
        {
            yield return $"'{SectionName}:{nameof(CertificatePath)}' requires '{SectionName}:{nameof(KeysPath)}'.";
        }

        string? failure;

        try
        {
            using var certificate = LoadCertificate();

            failure = certificate.HasPrivateKey
                ? null
                : $"The certificate at '{SectionName}:{nameof(CertificatePath)}' has no private key.";
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            // Without the exception message, which could repeat parts of the file.
            failure = $"The certificate at '{SectionName}:{nameof(CertificatePath)}' cannot be loaded ({exception.GetType().Name}). "
                + "It must be a PKCS#12 file with the private key, and the password must match.";
        }

        if (failure is not null)
        {
            yield return failure;
        }
    }

    /// <summary>
    ///     Applies the settings to Data Protection.
    /// </summary>
    /// <param name="builder">The builder of Data Protection.</param>
    public void Apply(IDataProtectionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!string.IsNullOrWhiteSpace(KeysPath))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(KeysPath));
        }

        if (!string.IsNullOrWhiteSpace(CertificatePath))
        {
            // Also registers the certificate to decrypt the keys. It is used for the lifetime of the application.
            builder.ProtectKeysWithCertificate(LoadCertificate());
        }
    }

    private X509Certificate2 LoadCertificate()
    {
        return X509CertificateLoader.LoadPkcs12FromFile(CertificatePath!, CertificatePassword);
    }
}
