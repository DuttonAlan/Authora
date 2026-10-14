// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Authora.Api.Security;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the security settings for production operation: headers, cookies, rate limits and the startup checks
///     of insecure configurations (US-036).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class SecurityHardeningTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Theory]
    [InlineData("/health")]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/api/users")]
    public async Task Responses_HaveSecurityHeaders(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(SecurityHeadersMiddleware.DefaultContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task SignInPage_KeepsItsOwnContentSecurityPolicy()
    {
        using var response = await _client.GetAsync("/account/login", TestContext.Current.CancellationToken);

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Contains("style-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AntiforgeryCookie_IsHostPrefixedSecureAndStrict()
    {
        using var response = await _client.GetAsync("/account/login", TestContext.Current.CancellationToken);

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith("__Host-authora_antiforgery=", StringComparison.Ordinal))
            .ToLowerInvariant();

        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenEndpoint_IsRateLimited()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:TokenPermitsPerMinute", "2"));
        using var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress });

        var statusCodes = new List<HttpStatusCode>();

        for (var request = 0; request < 3; request++)
        {
            using var content = new FormUrlEncodedContent([new("grant_type", "authorization_code")]);
            using var response = await client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
            statusCodes.Add(response.StatusCode);
        }

        // The first two are answered (with an OAuth error, as the Client is missing), the third is not.
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statusCodes);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DeployedEnvironment_WithInsecureSettings_DoesNotStart(string environmentName)
    {
        // appsettings.json allows every host.
        using var production = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environmentName));

        var exception = Assert.ThrowsAny<Exception>(() => production.Server);

        Assert.Contains("AllowedHosts", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://auth.example.com", "auth.example.com", "Issuer")]
    [InlineData("https://auth.example.com", "*", "AllowedHosts")]
    [InlineData("https://auth.example.com", "", "AllowedHosts")]
    [InlineData("https://auth.example.com", "auth.example.com;*", "AllowedHosts")]
    public void ProductionCheck_RejectsInsecureSettings(string issuer, string allowedHosts, string expected)
    {
        var failures = ProductionSettingsCheck.Validate(Configuration(issuer, allowedHosts));

        Assert.Contains(failures, failure => failure.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionCheck_AcceptsSecureSettings()
    {
        var failures = ProductionSettingsCheck.Validate(Configuration("https://auth.example.com", "auth.example.com"));

        Assert.Empty(failures);
    }

    [Fact]
    public void ForwardedHeaders_RejectInvalidProxies()
    {
        var settings = new ForwardedHeadersSettings { KnownProxies = ["proxy.local"], KnownNetworks = ["10.0.0.0/33"], ForwardLimit = 0 };

        Assert.Equal(3, settings.Validate().Count);
    }

    [Fact]
    public void ForwardedHeaders_AcceptValidProxies()
    {
        var settings = new ForwardedHeadersSettings { KnownProxies = ["10.0.0.5", "::1"], KnownNetworks = ["10.0.0.0/24"] };

        Assert.Empty(settings.Validate());
        Assert.True(settings.IsEnabled);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("0.0.0.0/7")]
    [InlineData("::/0")]
    [InlineData("::/6")]
    [InlineData("::/80")]
    [InlineData("::ffff:0:0/96")]
    [InlineData("::ffff:0:0/103")]
    public void ForwardedHeaders_RejectNetworksThatTrustAlmostEveryone(string network)
    {
        var settings = new ForwardedHeadersSettings { KnownNetworks = [network] };

        Assert.Contains(settings.Validate(), failure => failure.Contains("broader than", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("10.0.0.0/8")]
    [InlineData("172.16.0.0/12")]
    [InlineData("fc00::/7")]
    [InlineData("2001:db8::/32")]
    [InlineData("::ffff:10.0.0.0/104")]
    public void ForwardedHeaders_AcceptPrivateNetworks(string network)
    {
        var settings = new ForwardedHeadersSettings { KnownNetworks = [network] };

        Assert.Empty(settings.Validate());
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Prod", true)]
    public void ProductionCheck_AppliesToEveryEnvironmentExceptDevelopmentAndTesting(string environmentName, bool expected)
    {
        var environment = new HostingEnvironment { EnvironmentName = environmentName };

        Assert.Equal(expected, ProductionSettingsCheck.AppliesTo(environment));
    }

    [Fact]
    public void DataProtection_RejectsUnusableCertificate()
    {
        var withoutKeysPath = new DataProtectionSettings { CertificatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx") };

        var failures = withoutKeysPath.Validate().ToList();

        Assert.Contains(failures, failure => failure.Contains("requires", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("cannot be loaded", StringComparison.Ordinal));
    }

    [Fact]
    public void DataProtection_WithCertificate_EncryptsTheKeysAtRest()
    {
        var directory = Directory.CreateTempSubdirectory("authora-keys-");

        try
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Authora Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
                File.WriteAllBytes(Path.Combine(directory.FullName, "certificate.pfx"), certificate.Export(X509ContentType.Pkcs12, "secret"));
            }

            var settings = new DataProtectionSettings
            {
                KeysPath = Path.Combine(directory.FullName, "keys"),
                CertificatePath = Path.Combine(directory.FullName, "certificate.pfx"),
                CertificatePassword = "secret"
            };

            Assert.Empty(settings.Validate());

            var services = new ServiceCollection();
            settings.Apply(services.AddDataProtection().SetApplicationName("Authora"));

            using var provider = services.BuildServiceProvider();
            var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

            Assert.Equal("payload", protector.Unprotect(protector.Protect("payload")));

            var key = File.ReadAllText(Assert.Single(Directory.GetFiles(settings.KeysPath, "key-*.xml")));
            Assert.Contains("<encryptedSecret", key, StringComparison.Ordinal);
            Assert.DoesNotContain("<masterKey", key, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static IConfiguration Configuration(string issuer, string allowedHosts)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authora:OAuth:Issuer"] = issuer,
                ["AllowedHosts"] = allowedHosts
            })
            .Build();
    }
}
