// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Authora.Core.Application.Email;

namespace Authora.Infrastructure.Email;

/// <summary>
///     Checks the certificate of the SMTP server against <see cref="EmailOptions.TrustedCertificatePath"/> instead of
///     the system's trusted CAs.
/// </summary>
internal static class SmtpCertificateTrust
{
    /// <summary>
    ///     The Extended Key Usage <c>id-kp-serverAuth</c> (RFC 5280, Section 4.2.1.12).
    /// </summary>
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    ///     Loads the trusted certificate.
    /// </summary>
    /// <param name="path">The path from the settings.</param>
    /// <param name="contentRootPath">The directory relative paths are resolved against.</param>
    /// <returns>The certificate.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file is missing or no certificate.</exception>
    public static X509Certificate2 Load(string path, string contentRootPath)
    {
        var fullPath = Path.GetFullPath(path, contentRootPath);

        try
        {
            return X509CertificateLoader.LoadCertificateFromFile(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new InvalidOperationException($"The certificate file '{fullPath}' cannot be loaded: {exception.Message}", exception);
        }
    }

    /// <summary>
    ///     Whether the server certificate chains to the trusted certificate and is issued for the host.
    /// </summary>
    /// <param name="trusted">The only trusted root.</param>
    /// <param name="certificate">The certificate of the server.</param>
    /// <param name="chain">The chain the server sent, for its intermediate certificates.</param>
    /// <param name="errors">The errors found by the default validation, of which only the host name check is kept.</param>
    /// <returns><see langword="true"/> if the connection may be used.</returns>
    public static bool IsTrusted(X509Certificate2 trusted, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null
            || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)
            || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        using var serverCertificate = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        using var customChain = new X509Chain();

        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        customChain.ChainPolicy.CustomTrustStore.Add(trusted);
        // A private CA usually publishes no revocation lists.
        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        // Only certificates for servers, like the default validation: a certificate whose Extended Key Usage allows
        // only e.g. client authentication must not let its holder act as the SMTP server. A certificate without
        // Extended Key Usage is valid for every purpose.
        customChain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthenticationOid));

        if (chain is not null)
        {
            foreach (var element in chain.ChainElements)
            {
                customChain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        return customChain.Build(serverCertificate);
    }
}
