// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Application.Oidc;
using Authora.Infrastructure.Security;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the settings and key files of OpenID Connect are validated at startup (US-024), so a missing, weak
///     or wrong key stops the start instead of failing sign-ins. Needs no database.
/// </summary>
public sealed class SigningKeyFileTests : IDisposable
{
    private readonly string _contentRoot = Directory.CreateTempSubdirectory("authora-keys-").FullName;

    public void Dispose()
    {
        Directory.Delete(_contentRoot, recursive: true);
    }

    [Fact]
    public void SigningKeyAndAdditionalKey_AreLoadedAndPublishedInOrder()
    {
        using var signingRsa = RSA.Create(2048);
        using var previousRsa = RSA.Create(2048);
        var signingPath = WriteKey("signing.pem", signingRsa.ExportPkcs8PrivateKeyPem());
        var previousPath = WriteKey("previous.pem", previousRsa.ExportSubjectPublicKeyInfoPem());

        using var provider = CreateProvider(new OidcOptions { SigningKeyPath = signingPath, AdditionalKeyPaths = [previousPath] });

        Assert.True(provider.SigningKey.CanSign);
        Assert.Equal(2, provider.PublishedKeys.Count);
        Assert.Same(provider.SigningKey, provider.PublishedKeys[0]);
        Assert.False(provider.PublishedKeys[1].CanSign);
    }

    [Fact]
    public void RelativePath_IsResolvedAgainstContentRoot()
    {
        using var rsa = RSA.Create(2048);
        WriteKey("signing.pem", rsa.ExportPkcs8PrivateKeyPem());

        using var provider = CreateProvider(new OidcOptions { SigningKeyPath = "signing.pem" });

        Assert.True(provider.SigningKey.CanSign);
    }

    [Fact]
    public void SigningKeyWithOnlyPublicPart_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var path = WriteKey("signing.pem", rsa.ExportSubjectPublicKeyInfoPem());

        var exception = Assert.Throws<InvalidOperationException>(() => CreateProvider(new OidcOptions { SigningKeyPath = path }));

        Assert.Contains("only a public key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyShorterThan2048Bits_IsRejected()
    {
        using var rsa = RSA.Create(1024);
        var path = WriteKey("signing.pem", rsa.ExportPkcs8PrivateKeyPem());

        var exception = Assert.Throws<InvalidOperationException>(() => CreateProvider(new OidcOptions { SigningKeyPath = path }));

        Assert.Contains("at least 2048 bits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EncryptedKey_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportEncryptedPkcs8PrivateKeyPem(
            "password",
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
        var path = WriteKey("signing.pem", pem);

        var exception = Assert.Throws<InvalidOperationException>(() => CreateProvider(new OidcOptions { SigningKeyPath = path }));

        Assert.Contains("unencrypted RSA key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonRsaKey_IsRejected()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var path = WriteKey("signing.pem", ecdsa.ExportPkcs8PrivateKeyPem());

        var exception = Assert.Throws<InvalidOperationException>(() => CreateProvider(new OidcOptions { SigningKeyPath = path }));

        Assert.Contains("unencrypted RSA key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameKeyConfiguredTwice_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var signingPath = WriteKey("signing.pem", rsa.ExportPkcs8PrivateKeyPem());
        var publicPath = WriteKey("signing-public.pem", rsa.ExportSubjectPublicKeyInfoPem());

        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateProvider(new OidcOptions { SigningKeyPath = signingPath, AdditionalKeyPaths = [publicPath] }));

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingFile_IsRejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateProvider(new OidcOptions { SigningKeyPath = Path.Combine(_contentRoot, "missing.pem") }));

        Assert.Contains("cannot be read", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeysValidator_ReportsInvalidKeyAsFailureInsteadOfThrowing()
    {
        using var rsa = RSA.Create(2048);
        var path = WriteKey("signing.pem", rsa.ExportSubjectPublicKeyInfoPem());

        var result = new OidcSigningKeysValidator(new TestHostEnvironment(_contentRoot))
            .Validate(null, new OidcOptions { SigningKeyPath = path });

        Assert.True(result.Failed);
        Assert.Contains("only a public key", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void KeysValidator_SkipsSettingsThatOptionsValidatorReports()
    {
        var result = new OidcSigningKeysValidator(new TestHostEnvironment(_contentRoot))
            .Validate(null, new OidcOptions { SigningKeyPath = " " });

        Assert.True(result.Skipped);
    }

    [Fact]
    public void OptionsValidator_AcceptsDefaultsWithSigningKeyPath()
    {
        var result = new OidcOptionsValidator().Validate(null, new OidcOptions { SigningKeyPath = "signing.pem" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void OptionsValidator_RejectsMissingSigningKeyPath()
    {
        var result = new OidcOptionsValidator().Validate(null, new OidcOptions());

        Assert.True(result.Failed);
        Assert.Contains("SigningKeyPath", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsValidator_RejectsEmptyAdditionalKeyPath()
    {
        var result = new OidcOptionsValidator().Validate(
            null,
            new OidcOptions { SigningKeyPath = "signing.pem", AdditionalKeyPaths = [""] });

        Assert.True(result.Failed);
        Assert.Contains("AdditionalKeyPaths", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    [InlineData("01:00:01")]
    public void OptionsValidator_RejectsIdTokenLifetimeOutOfRange(string lifetime)
    {
        var result = new OidcOptionsValidator().Validate(
            null,
            new OidcOptions { SigningKeyPath = "signing.pem", IdTokenLifetime = TimeSpan.Parse(lifetime, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.True(result.Failed);
        Assert.Contains("IdTokenLifetime", result.FailureMessage, StringComparison.Ordinal);
    }

    private PemSigningKeyProvider CreateProvider(OidcOptions options)
    {
        return new PemSigningKeyProvider(
            Options.Create(options),
            new TestHostEnvironment(_contentRoot),
            NullLogger<PemSigningKeyProvider>.Instance);
    }

    private string WriteKey(string fileName, string pem)
    {
        var path = Path.Combine(_contentRoot, fileName);
        File.WriteAllText(path, pem);

        return path;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "Authora.Api";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
