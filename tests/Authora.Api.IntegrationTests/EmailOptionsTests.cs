// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Security;
using Authora.Core.Abstractions;
using Authora.Core.Application.Email;
using Authora.Infrastructure.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the validation of the email settings, and that Authora refuses unencrypted SMTP outside development.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class EmailOptionsTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    [Fact]
    public void Validator_AcceptsDefaults_AndDisablesEmail()
    {
        var options = new EmailOptions();

        Assert.True(new EmailOptionsValidator().Validate(null, options).Succeeded);
        Assert.False(options.IsEnabled);
    }

    [Fact]
    public void Validator_AcceptsCompleteSettings()
    {
        var result = new EmailOptionsValidator().Validate(null, Valid());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(SmtpTlsMode.StartTls, 587)]
    [InlineData(SmtpTlsMode.Implicit, 465)]
    [InlineData(SmtpTlsMode.None, 25)]
    public void EffectivePort_DefaultsToThePortOfTheTlsMode(SmtpTlsMode tlsMode, int expected)
    {
        Assert.Equal(expected, new EmailOptions { TlsMode = tlsMode }.EffectivePort);
        Assert.Equal(2525, new EmailOptions { TlsMode = tlsMode, Port = 2525 }.EffectivePort);
    }

    [Fact]
    public void Validator_RequiresSenderAddressWhenHostIsSet()
    {
        var options = Valid();
        options.SenderAddress = null;

        AssertFails(options, nameof(EmailOptions.SenderAddress));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("Authora <no-reply@example.com>")]
    [InlineData("a@example.com, b@example.com")]
    public void Validator_RejectsInvalidSenderAddress(string senderAddress)
    {
        var options = Valid();
        options.SenderAddress = senderAddress;

        AssertFails(options, nameof(EmailOptions.SenderAddress));
    }

    [Theory]
    [InlineData("smtp://smtp.example.com")]
    [InlineData("smtp.example.com:587")]
    [InlineData("smtp example com")]
    public void Validator_RejectsInvalidHost(string host)
    {
        var options = Valid();
        options.Host = host;

        AssertFails(options, nameof(EmailOptions.Host));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validator_RejectsPortOutOfRange(int port)
    {
        var options = Valid();
        options.Port = port;

        AssertFails(options, nameof(EmailOptions.Port));
    }

    [Fact]
    public void Validator_RejectsUnknownTlsMode()
    {
        var options = Valid();
        options.TlsMode = (SmtpTlsMode)42;

        AssertFails(options, nameof(EmailOptions.TlsMode));
    }

    [Theory]
    [InlineData("user", null)]
    [InlineData(null, "password")]
    public void Validator_RequiresUsernameAndPasswordTogether(string? username, string? password)
    {
        var options = Valid();
        options.Username = username;
        options.Password = password;

        AssertFails(options, nameof(EmailOptions.Username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Authora\r\nBcc: victim@example.com")]
    public void Validator_RejectsInvalidSenderName(string senderName)
    {
        var options = Valid();
        options.SenderName = senderName;

        AssertFails(options, nameof(EmailOptions.SenderName));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:05:01")]
    [InlineData("30")]
    public void Validator_RejectsTimeoutOutOfRange(string timeout)
    {
        var options = Valid();
        options.Timeout = TimeSpan.Parse(timeout, System.Globalization.CultureInfo.InvariantCulture);

        AssertFails(options, nameof(EmailOptions.Timeout));
    }

    [Fact]
    public void Validator_RejectsTrustedCertificateWithoutTls()
    {
        var options = Valid();
        options.TlsMode = SmtpTlsMode.None;
        options.TrustedCertificatePath = "smtp.crt";

        AssertFails(options, nameof(EmailOptions.TrustedCertificatePath));
    }

    [Fact]
    public void CertificateValidator_RejectsMissingFile()
    {
        var options = Valid();
        options.TrustedCertificatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.crt");

        var result = new EmailCertificateValidator(new HostingEnvironment { ContentRootPath = Path.GetTempPath() })
            .Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(nameof(EmailOptions.TrustedCertificatePath), result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WithIncompleteSettings_DoesNotStart()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:Email:Host", "smtp.example.com"));

        var exception = Assert.ThrowsAny<Exception>(() => misconfigured.Server);

        Assert.Contains("Authora:Email:SenderAddress", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("2")]
    public void ProductionCheck_RejectsUnencryptedSmtp(string tlsMode)
    {
        var failures = ProductionSettingsCheck.Validate(Configuration(new()
        {
            ["Authora:Email:Host"] = "smtp.example.com",
            ["Authora:Email:TlsMode"] = tlsMode
        }));

        Assert.Contains(failures, failure => failure.Contains("Authora:Email:TlsMode", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("smtp.example.com", "StartTls")]
    [InlineData("smtp.example.com", "Implicit")]
    [InlineData("smtp.example.com", null)]
    [InlineData(null, "None")]
    public void ProductionCheck_AcceptsEncryptedOrDisabledSmtp(string? host, string? tlsMode)
    {
        var failures = ProductionSettingsCheck.Validate(Configuration(new()
        {
            ["Authora:Email:Host"] = host,
            ["Authora:Email:TlsMode"] = tlsMode
        }));

        Assert.Empty(failures);
    }

    [Fact]
    public async Task SmtpSender_WithoutHost_IsDisabledAndRefusesToSend()
    {
        using var sender = new SmtpEmailSender(
            Options.Create(new EmailOptions()),
            new HostingEnvironment { ContentRootPath = Path.GetTempPath() },
            NullLogger<SmtpEmailSender>.Instance);

        Assert.False(sender.IsEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new EmailMessage("someone@example.com", null, "Subject", "Text", "<p>Html</p>"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Api_UsesTheFakeSenderInTests()
    {
        Assert.Same(factory.EmailSender, factory.Services.GetService(typeof(IEmailSender)));
    }

    private static EmailOptions Valid() => new()
    {
        Host = "smtp.example.com",
        SenderAddress = "no-reply@example.com",
        Username = "authora",
        Password = "secret"
    };

    private static void AssertFails(EmailOptions options, string setting)
    {
        var result = new EmailOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"'{EmailOptions.SectionName}:{setting}'", result.FailureMessage, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> email)
    {
        email["Authora:OAuth:Issuer"] = "https://auth.example.com";
        email["AllowedHosts"] = "auth.example.com";

        return new ConfigurationBuilder().AddInMemoryCollection(email).Build();
    }
}
