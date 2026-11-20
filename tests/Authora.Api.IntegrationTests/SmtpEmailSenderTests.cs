// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

using Authora.Core.Application.Email;
using Authora.Infrastructure.Email;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Sends real emails to a Mailpit container that requires STARTTLS with a self-signed certificate.
/// </summary>
/// <param name="mailpit">The SMTP server.</param>
public sealed class SmtpEmailSenderTests(SmtpEmailSenderTests.MailpitFixture mailpit) : IClassFixture<SmtpEmailSenderTests.MailpitFixture>
{
    [Fact]
    public async Task Send_WithStartTlsAndTrustedCertificate_Delivers()
    {
        var subject = $"Test {Guid.NewGuid():N}";

        using (var sender = CreateSender(mailpit.Options(trustCertificate: true)))
        {
            Assert.True(sender.IsEnabled);

            await sender.SendAsync(
                new EmailMessage("ada@example.com", "Ada Lovelace", subject, "Plain text", "<p>HTML</p>"),
                TestContext.Current.CancellationToken);
        }

        var message = await mailpit.FindAsync(subject);

        Assert.Equal("no-reply@example.com", message.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal("Authora Tests", message.GetProperty("From").GetProperty("Name").GetString());
        Assert.Equal("ada@example.com", message.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.EndsWith("@example.com", message.GetProperty("MessageID").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_WithUntrustedCertificate_Fails()
    {
        using var sender = CreateSender(mailpit.Options(trustCertificate: false));

        await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage("ada@example.com", null, "Untrusted", "Plain text", "<p>HTML</p>"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Send_WithoutTls_ToServerThatRequiresStartTls_Fails()
    {
        var options = mailpit.Options(trustCertificate: false);
        options.TlsMode = SmtpTlsMode.None;

        using var sender = CreateSender(options);

        await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage("ada@example.com", null, "Unencrypted", "Plain text", "<p>HTML</p>"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Send_ToRejectedRecipient_KeepsTheAddressesOutOfLogsAndMessage()
    {
        var logger = new CapturingLogger<SmtpEmailSender>();
        using var sender = CreateSender(mailpit.Options(trustCertificate: true), logger);

        // Mailpit only accepts recipients at example.com and quotes the address in its reply.
        var exception = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage("victim@blocked.test", null, "Rejected", "Plain text", "<p>HTML</p>"),
            TestContext.Current.CancellationToken));

        Assert.Contains("RecipientNotAccepted", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("blocked.test", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no-reply@example.com", exception.Message, StringComparison.Ordinal);

        var failure = Assert.Single(logger.Entries, entry => entry.EventId == 14004);
        Assert.Contains("RecipientNotAccepted", failure.Message, StringComparison.Ordinal);
        Assert.All(logger.Entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain("blocked.test", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("no-reply@example.com", entry.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(SmtpCertificateTrust.ServerAuthenticationOid, true)]
    [InlineData("1.3.6.1.5.5.7.3.2", false)] // Client authentication only.
    public void Trust_RequiresACertificateForServers(string? extendedKeyUsage, bool expected)
    {
        using var certificate = CreateCertificate("localhost", extendedKeyUsage);

        // The trusted certificate is the server's own, which the default validation reports as an untrusted root.
        Assert.Equal(
            expected,
            SmtpCertificateTrust.IsTrusted(certificate, certificate, chain: null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Trust_RejectsCertificateForAnotherHost()
    {
        using var certificate = CreateCertificate("localhost", extendedKeyUsage: null);

        Assert.False(SmtpCertificateTrust.IsTrusted(
            certificate,
            certificate,
            chain: null,
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Trust_RejectsCertificateNotIssuedByTheTrustedOne()
    {
        using var trusted = CreateCertificate("localhost", extendedKeyUsage: null);
        using var other = CreateCertificate("localhost", extendedKeyUsage: null);

        Assert.False(SmtpCertificateTrust.IsTrusted(trusted, other, chain: null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    private static X509Certificate2 CreateCertificate(string host, string? extendedKeyUsage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (extendedKeyUsage is not null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(extendedKeyUsage)], critical: false));
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static SmtpEmailSender CreateSender(EmailOptions options, ILogger<SmtpEmailSender>? logger = null)
    {
        return new SmtpEmailSender(
            Options.Create(options),
            new HostingEnvironment { ContentRootPath = Path.GetTempPath() },
            logger ?? NullLogger<SmtpEmailSender>.Instance);
    }

    /// <summary>
    ///     Keeps the formatted log messages of a test.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<(int EventId, string Message, Exception? Exception)> _entries = [];

        public IReadOnlyList<(int EventId, string Message, Exception? Exception)> Entries
        {
            get
            {
                lock (_entries)
                {
                    return [.. _entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
            {
                _entries.Add((eventId.Id, formatter(state, exception), exception));
            }
        }
    }

    /// <summary>
    ///     Runs Mailpit with a self-signed certificate for <c>localhost</c>, and reads the received emails from its API.
    /// </summary>
    public sealed class MailpitFixture : IAsyncLifetime
    {
        private const int SmtpPort = 1025;

        private const int HttpPort = 8025;

        private readonly string _directory = Directory.CreateTempSubdirectory("authora-mailpit-").FullName;

        private IContainer? _container;

        private string CertificatePath => Path.Combine(_directory, "smtp.crt");

        /// <summary>
        ///     Settings for the container.
        /// </summary>
        /// <param name="trustCertificate">Whether the self-signed certificate is trusted.</param>
        /// <returns>The settings.</returns>
        public EmailOptions Options(bool trustCertificate) => new()
        {
            Host = "localhost",
            Port = _container!.GetMappedPublicPort(SmtpPort),
            TlsMode = SmtpTlsMode.StartTls,
            Username = "authora",
            Password = "secret",
            SenderAddress = "no-reply@example.com",
            SenderName = "Authora Tests",
            Timeout = TimeSpan.FromSeconds(10),
            TrustedCertificatePath = trustCertificate ? CertificatePath : null
        };

        /// <summary>
        ///     Waits until Mailpit has received the email with a subject.
        /// </summary>
        /// <param name="subject">The subject.</param>
        /// <returns>The summary of the email from Mailpit's API.</returns>
        public async Task<JsonElement> FindAsync(string subject)
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri($"http://localhost:{_container!.GetMappedPublicPort(HttpPort)}")
            };

            for (var attempt = 0; attempt < 50; attempt++)
            {
                var response = await client.GetFromJsonAsync<JsonElement>(
                    new Uri("/api/v1/messages", UriKind.Relative), TestContext.Current.CancellationToken);

                foreach (var message in response.GetProperty("messages").EnumerateArray())
                {
                    if (message.GetProperty("Subject").GetString() == subject)
                    {
                        return message;
                    }
                }

                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            throw new InvalidOperationException($"Mailpit did not receive the email '{subject}'.");
        }

        /// <inheritdoc/>
        public async ValueTask InitializeAsync()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            // A CA certificate, like the one `openssl req -x509` creates for the mail catcher of docker-compose.yml.
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            var certificatePem = certificate.ExportCertificatePem();

            await File.WriteAllTextAsync(CertificatePath, certificatePem);

            _container = new ContainerBuilder("axllent/mailpit:v1.33.0")
                .WithPortBinding(SmtpPort, assignRandomHostPort: true)
                .WithPortBinding(HttpPort, assignRandomHostPort: true)
                .WithResourceMapping(System.Text.Encoding.ASCII.GetBytes(certificatePem), "/certs/smtp.crt")
                .WithResourceMapping(System.Text.Encoding.ASCII.GetBytes(key.ExportPkcs8PrivateKeyPem()), "/certs/smtp.key")
                .WithEnvironment("MP_SMTP_TLS_CERT", "/certs/smtp.crt")
                .WithEnvironment("MP_SMTP_TLS_KEY", "/certs/smtp.key")
                .WithEnvironment("MP_SMTP_REQUIRE_STARTTLS", "true")
                .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
                .WithEnvironment("MP_SMTP_ALLOWED_RECIPIENTS", @"@example\.com$")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(wait => wait.ForPort(HttpPort).ForPath("/readyz")))
                .Build();

            await _container.StartAsync();
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            Directory.Delete(_directory, recursive: true);
        }
    }
}
