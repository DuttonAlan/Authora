// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Text.RegularExpressions;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a signed-in User whose Email is not verified can send themselves a new verification link: the notice
///     on the account overview and after sign-in, the limits per User and per IP address, and the case without email
///     delivery.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed partial class RequestEmailVerificationTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // Cookies are sent explicitly, so each request uses exactly the Session of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Page_WithoutSession_RedirectsToSignIn()
    {
        using var response = await _client.GetAsync(RequestEmailVerificationModel.PagePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(SessionAuthenticationHandler.SignInPath, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnverifiedUser_SeesTheNotice_OnTheOverviewAndAfterSignIn()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        var (securityStatus, security) = await GetAsync(_client, SecurityModel.PagePath, session);
        var (loginStatus, login) = await GetAsync(_client, SessionAuthenticationHandler.SignInPath, session);

        Assert.Equal(HttpStatusCode.OK, securityStatus);
        Assert.Equal(HttpStatusCode.OK, loginStatus);
        Assert.Contains(NoticeForm, security, StringComparison.Ordinal);
        Assert.Contains(NoticeForm, login, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifiedUser_SeesNoNotice()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        await MarkVerifiedInDatabaseAsync(userId);

        var (_, security) = await GetAsync(_client, SecurityModel.PagePath, session);
        var (_, login) = await GetAsync(_client, SessionAuthenticationHandler.SignInPath, session);
        var (_, page) = await GetAsync(_client, RequestEmailVerificationModel.PagePath, session);

        Assert.DoesNotContain(NoticeForm, security, StringComparison.Ordinal);
        Assert.DoesNotContain(NoticeForm, login, StringComparison.Ordinal);
        Assert.Contains(RequestEmailVerificationModel.AlreadyVerifiedMessage, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_SendsANewLink_ThatReplacesTheEarlierOne_AndVerifiesTheEmail()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);
        var antiforgery = await ReadAntiforgeryAsync(_client, session);

        var (firstStatus, first) = await RequestAsync(_client, session);
        var (secondStatus, second) = await RequestAsync(_client, session);

        Assert.Equal(HttpStatusCode.OK, firstStatus);
        Assert.Equal(HttpStatusCode.OK, secondStatus);
        Assert.Contains(RequestEmailVerificationModel.SentMessage, first, StringComparison.Ordinal);
        Assert.Contains(RequestEmailVerificationModel.SentMessage, second, StringComparison.Ordinal);

        var messages = factory.EmailSender.SentTo(email);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Confirm your email address", messages[^1].Subject);

        // Only the newest link works.
        var (_, oldLink) = await ConfirmAsync(_client, ReadToken(messages[0]));
        var (_, newLink) = await ConfirmAsync(_client, ReadToken(messages[1]));

        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, oldLink, StringComparison.Ordinal);
        Assert.Contains(VerifyEmailModel.VerifiedMessage, newLink, StringComparison.Ordinal);
        Assert.True((await GetUserAsync(userId)).IsEmailVerified);

        var events = await GetEventsAsync(AuditEventTypes.Account.EmailVerificationRequested, userId);

        Assert.Equal(2, events.Count);
        Assert.All(events, auditEvent =>
        {
            Assert.Equal(AuditOutcome.Succeeded, auditEvent.Outcome);
            Assert.Equal(userId, auditEvent.ActorUserId);
            Assert.StartsWith("Link valid until ", auditEvent.Details, StringComparison.Ordinal);
        });

        // Once verified, the page shows no form, and a submitted one sends nothing.
        var (_, afterwards) = await PostAsync(_client, session, antiforgery);

        Assert.Contains(RequestEmailVerificationModel.AlreadyVerifiedMessage, afterwards, StringComparison.Ordinal);
        Assert.Equal(2, factory.EmailSender.SentTo(email).Count);
        Assert.Equal(2, (await GetEventsAsync(AuditEventTypes.Account.EmailVerificationRequested, userId)).Count);
    }

    [Fact]
    public async Task Request_IsLimitedPerUser_CountingLinksSentOtherwise()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:EmailVerification:MaxEmailsPerHour", "2"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        // A link an administrator or the automatic email sent counts as well.
        await AddVerificationTokenAsync(userId);

        var (_, first) = await RequestAsync(client, session);
        var (status, second) = await RequestAsync(client, session);

        Assert.Contains(RequestEmailVerificationModel.SentMessage, first, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(RequestEmailVerificationModel.TooManyLinksMessage, second, StringComparison.Ordinal);
        Assert.Single(factory.EmailSender.SentTo(email));
        Assert.Equal(2, await CountTokensAsync(userId));

        var events = await GetEventsAsync(AuditEventTypes.Account.EmailVerificationRequested, userId);

        Assert.Equal([AuditOutcome.Succeeded, AuditOutcome.Failed], events.OrderBy(auditEvent => auditEvent.OccurredAt).Select(auditEvent => auditEvent.Outcome));
        Assert.Equal("Too many verification links in the last hour", events.Single(auditEvent => auditEvent.Outcome == AuditOutcome.Failed).Details);
    }

    [Fact]
    public async Task Request_IsRateLimitedPerIpAddress()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:EmailVerificationPermitsPerMinute", "1"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (firstUserId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (secondUserId, secondEmail) = await factory.CreateUserWithPasswordAsync(Password);

        var (first, _) = await RequestAsync(client, await factory.CreateSessionAsync(firstUserId));
        var (second, _) = await RequestAsync(client, await factory.CreateSessionAsync(secondUserId));

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.TooManyRequests, second);
        Assert.Empty(factory.EmailSender.SentTo(secondEmail));
    }

    [Fact]
    public async Task DeliveryFailure_IsShown_AndRecorded()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        factory.EmailSender.Fail = true;

        try
        {
            var (status, html) = await RequestAsync(_client, session);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains(RequestEmailVerificationModel.DeliveryFailedMessage, html, StringComparison.Ordinal);
        }
        finally
        {
            factory.EmailSender.Fail = false;
        }

        Assert.Empty(factory.EmailSender.SentTo(email));

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.EmailVerificationRequested, userId));

        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.Equal("The email could not be sent", failed.Details);
    }

    [Fact]
    public async Task WithoutEmailDelivery_NothingIsOffered()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        // The antiforgery token is read while delivery still works, so the POST is only rejected for the missing delivery.
        var antiforgery = await ReadAntiforgeryAsync(_client, session);

        factory.EmailSender.IsEnabled = false;

        try
        {
            var (securityStatus, security) = await GetAsync(_client, SecurityModel.PagePath, session);
            var (_, login) = await GetAsync(_client, SessionAuthenticationHandler.SignInPath, session);
            var (pageStatus, _) = await GetAsync(_client, RequestEmailVerificationModel.PagePath, session);
            var (postStatus, _) = await PostAsync(_client, session, antiforgery);

            Assert.Equal(HttpStatusCode.OK, securityStatus);
            Assert.DoesNotContain(NoticeForm, security, StringComparison.Ordinal);
            Assert.DoesNotContain(NoticeForm, login, StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.NotFound, pageStatus);
            Assert.Equal(HttpStatusCode.NotFound, postStatus);
        }
        finally
        {
            factory.EmailSender.IsEnabled = true;
        }

        Assert.Equal(0, await CountTokensAsync(userId));
        Assert.Empty(await GetEventsAsync(AuditEventTypes.Account.EmailVerificationRequested, userId));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(EmailVerificationOptions.MaxEmailsPerHourLimit, true)]
    [InlineData(EmailVerificationOptions.MaxEmailsPerHourLimit + 1, false)]
    public void OptionsValidator_LimitsTheEmailsPerHour(int maxEmailsPerHour, bool valid)
    {
        var options = new EmailVerificationOptions { MaxEmailsPerHour = maxEmailsPerHour };

        Assert.Equal(valid, new EmailVerificationOptionsValidator().Validate(null, options).Succeeded);
    }

    private const string NoticeForm = $"action=\"{RequestEmailVerificationModel.PagePath}\"";

    [GeneratedRegex(@"https://\S+/account/verify-email\?token=(?<token>[^\s""&<]+)")]
    private static partial Regex LinkPattern();

    private static string ReadToken(EmailMessage message)
    {
        var match = LinkPattern().Match(message.TextBody);

        Assert.True(match.Success, "The email contains no verification link.");

        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetAsync(HttpClient client, string path, string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // Opens the page like a browser, then submits the form.
    private static async Task<(HttpStatusCode Status, string Html)> RequestAsync(HttpClient client, string sessionToken)
    {
        return await PostAsync(client, sessionToken, await ReadAntiforgeryAsync(client, sessionToken));
    }

    private static async Task<(string Token, string Cookie)> ReadAntiforgeryAsync(HttpClient client, string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RequestEmailVerificationModel.PagePath);
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var page = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Send a new link</button>", await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        return await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
    }

    private static async Task<(HttpStatusCode Status, string Html)> PostAsync(
        HttpClient client,
        string sessionToken,
        (string Token, string Cookie) antiforgery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RequestEmailVerificationModel.PagePath)
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", antiforgery.Token)])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgery.Cookie}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // Opens the link like a browser, then submits the form.
    private static async Task<(HttpStatusCode Status, string Html)> ConfirmAsync(HttpClient client, string token)
    {
        var path = $"{EmailVerificationLink.PagePath}?{EmailVerificationLink.TokenParameter}={Uri.EscapeDataString(token)}";

        using var page = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, EmailVerificationLink.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new(EmailVerificationLink.TokenParameter, token),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task AddVerificationTokenAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        var secretHasher = scope.ServiceProvider.GetRequiredService<Core.Abstractions.ISecretHasher>();

        context.EmailTokens.Add(EmailToken.Create(
            secretHasher.Hash(Guid.NewGuid().ToString("N")), user, EmailTokenPurpose.EmailVerification, DateTime.UtcNow, TimeSpan.FromHours(1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task MarkVerifiedInDatabaseAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        user.MarkEmailVerified(DateTime.UtcNow);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<User> GetUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountTokensAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.EmailTokens.CountAsync(token => token.UserId == userId, TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetEventsAsync(string type, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == userId.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
