// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Email;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the password reset by email: the "Forgot password?" page, the reset link, its limits, and the reset sent
///     by an administrator.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed partial class PasswordResetTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private const string NewPassword = "a-brand-new-long-password";

    // Cookies are sent explicitly, so each request uses exactly the Session of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task SignInPage_LinksToForgotPassword()
    {
        var html = await _client.GetStringAsync(new Uri("/account/login", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains($"href=\"{ForgotPasswordModel.PagePath}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_SetsNewPassword_RevokesSessionsAndTokens_AndEndsTheLockout()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);
        var accessToken = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // One failed attempt with a limit of one locks the User out.
            await scope.ServiceProvider.GetRequiredService<IUserRepository>()
                .RecordFailedSignIn(userId, DateTime.UtcNow, 1, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        }

        var (requestStatus, requestHtml) = await RequestResetAsync(_client, email);

        Assert.Equal(HttpStatusCode.OK, requestStatus);
        Assert.Contains(ForgotPasswordModel.SentMessage, requestHtml, StringComparison.Ordinal);

        var resetEmail = Assert.Single(await factory.EmailSender.WaitForAsync(email));

        Assert.Equal("Reset your password", resetEmail.Subject);

        var token = ReadToken(resetEmail);
        var page = await GetResetPageAsync(token);

        Assert.Contains("Set password", page, StringComparison.Ordinal);

        var (status, html) = await ResetAsync(_client, token, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Your new password is set.", html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, NewPassword));
        Assert.False(await HasPasswordAsync(userId, Password));

        // Every Session and every token ends.
        Assert.Equal(HttpStatusCode.Redirect, await GetPasswordPageStatusAsync(session));

        using (var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var userInfo = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
        }

        var user = await GetUserAsync(userId);

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);

        var requested = Assert.Single(await WaitForEventsAsync(AuditEventTypes.Account.PasswordResetRequested, userId));
        var reset = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.PasswordReset, userId));

        Assert.Equal(AuditOutcome.Succeeded, requested.Outcome);
        Assert.Equal(AuditOutcome.Succeeded, reset.Outcome);
        Assert.DoesNotContain(token, requested.Details ?? string.Empty, StringComparison.Ordinal);

        var notification = (await factory.EmailSender.WaitForAsync(email, 2))[1];

        Assert.Equal("Your password was reset", notification.Subject);
        Assert.DoesNotContain(NewPassword, notification.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_ForUnknownAddress_AnswersTheSame_AndSendsNothing()
    {
        var unknown = $"{Guid.NewGuid():N}@example.com";
        var (_, known) = await factory.CreateUserWithPasswordAsync(Password);

        var (unknownStatus, unknownHtml) = await RequestResetAsync(_client, unknown);
        var (knownStatus, knownHtml) = await RequestResetAsync(_client, known);

        Assert.Equal(HttpStatusCode.OK, unknownStatus);
        Assert.Equal(knownStatus, unknownStatus);
        Assert.Contains(ForgotPasswordModel.SentMessage, unknownHtml, StringComparison.Ordinal);
        Assert.Contains(ForgotPasswordModel.SentMessage, knownHtml, StringComparison.Ordinal);

        // The queue sends in order, so once the second email arrived, the first request sent nothing.
        await factory.EmailSender.WaitForAsync(known);

        Assert.Empty(factory.EmailSender.SentTo(unknown));
    }

    [Fact]
    public async Task Request_ForDeactivatedUser_SendsNothing()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            (await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Deactivate();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (status, html) = await RequestResetAsync(_client, email);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(ForgotPasswordModel.SentMessage, html, StringComparison.Ordinal);

        // Handled in the background: once the event is recorded, the request is done.
        var failed = Assert.Single(await WaitForEventsAsync(AuditEventTypes.Account.PasswordResetRequested, userId));

        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.Equal(0, await CountTokensAsync(userId));
    }

    [Fact]
    public async Task OpenLink_StopsWorking_WhenThePasswordChanges()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await RequestResetAsync(_client, email);

        var token = ReadToken(Assert.Single(await factory.EmailSender.WaitForAsync(email)));

        // E.g. the User changed the password at /account/password, or an administrator set one.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            user.SetPasswordHash(passwordHasher.Hash("a-password-set-elsewhere"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync(token), StringComparison.Ordinal);

        var (status, html) = await PostResetAsync(_client, token, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, "a-password-set-elsewhere"));
    }

    [Fact]
    public async Task Request_WhenDeliveryFails_IsRecordedAsFailed()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        factory.EmailSender.Fail = true;

        try
        {
            var (status, html) = await RequestResetAsync(_client, email);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains(ForgotPasswordModel.SentMessage, html, StringComparison.Ordinal);

            var failed = Assert.Single(await WaitForEventsAsync(AuditEventTypes.Account.PasswordResetRequested, userId));

            Assert.Equal(AuditOutcome.Failed, failed.Outcome);
            Assert.Equal("The email could not be sent", failed.Details);
        }
        finally
        {
            factory.EmailSender.Fail = false;
        }

        Assert.Empty(factory.EmailSender.SentTo(email));
    }

    [Fact]
    public async Task UsedLink_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await RequestResetAsync(_client, email);

        var token = ReadToken(Assert.Single(await factory.EmailSender.WaitForAsync(email)));
        var (_, first) = await ResetAsync(_client, token, NewPassword, NewPassword);

        Assert.Contains("Your new password is set.", first, StringComparison.Ordinal);

        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync(token), StringComparison.Ordinal);

        // Posted directly, without opening the page first.
        var (status, second) = await PostResetAsync(_client, token, "yet-another-long-password", "yet-another-long-password");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, second, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, NewPassword));

        var events = await GetEventsAsync(AuditEventTypes.Account.PasswordReset, userId);

        Assert.Equal(AuditOutcome.Succeeded, Assert.Single(events).Outcome);
    }

    [Fact]
    public async Task ExpiredLink_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var token = Guid.NewGuid().ToString("N");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            // Issued two hours ago with a lifetime of one hour.
            context.EmailTokens.Add(EmailToken.Create(
                secretHasher.Hash(token), user, EmailTokenPurpose.PasswordReset, DateTime.UtcNow.AddHours(-2), TimeSpan.FromHours(1)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync(token), StringComparison.Ordinal);

        var (status, html) = await PostResetAsync(_client, token, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, Password));
    }

    [Fact]
    public async Task UnknownLink_IsRejected()
    {
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync("not-a-token"), StringComparison.Ordinal);
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync(new string('a', 500)), StringComparison.Ordinal);

        var (status, html) = await PostResetAsync(_client, "not-a-token", NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewerLink_ReplacesTheOlderOne()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await RequestResetAsync(_client, email);
        await RequestResetAsync(_client, email);

        var emails = await factory.EmailSender.WaitForAsync(email, 2);
        var older = ReadToken(emails[0]);
        var newer = ReadToken(emails[1]);

        Assert.Contains(ResetPasswordModel.InvalidLinkMessage, await GetResetPageAsync(older), StringComparison.Ordinal);

        var (_, html) = await ResetAsync(_client, newer, NewPassword, NewPassword);

        Assert.Contains("Your new password is set.", html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, NewPassword));
    }

    [Theory]
    [InlineData("too-short", "too-short", "The new password must be 12 to 128 characters long.")]
    [InlineData(NewPassword, "a-different-new-password", "The new passwords do not match.")]
    public async Task InvalidNewPassword_KeepsTheLinkUsable(string newPassword, string confirmPassword, string message)
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await RequestResetAsync(_client, email);

        var token = ReadToken(Assert.Single(await factory.EmailSender.WaitForAsync(email)));
        var (status, html) = await ResetAsync(_client, token, newPassword, confirmPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.Contains("Set password", html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, Password));

        var (_, retried) = await ResetAsync(_client, token, NewPassword, NewPassword);

        Assert.Contains("Your new password is set.", retried, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_IsLimitedPerAddress()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:PasswordReset:MaxEmailsPerHour", "1"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        var (first, _) = await RequestResetAsync(client, email);
        var (second, html) = await RequestResetAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.OK, second);
        Assert.Contains(ForgotPasswordModel.SentMessage, html, StringComparison.Ordinal);

        var events = await WaitForEventsAsync(AuditEventTypes.Account.PasswordResetRequested, userId, 2);

        Assert.Equal([AuditOutcome.Succeeded, AuditOutcome.Failed], events.OrderBy(auditEvent => auditEvent.OccurredAt).Select(auditEvent => auditEvent.Outcome));
        Assert.Equal(1, await CountTokensAsync(userId));
    }

    [Fact]
    public async Task Request_IsRateLimitedPerIpAddress()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:PasswordResetPermitsPerMinute", "1"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (first, _) = await RequestResetAsync(client, $"{Guid.NewGuid():N}@example.com");
        var (second, _) = await RequestResetAsync(client, $"{Guid.NewGuid():N}@example.com");

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.TooManyRequests, second);
    }

    [Fact]
    public async Task SelfService_CanBeDisabled()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:PasswordReset:SelfServiceEnabled", "false"));
        using var client = disabled.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        using var page = await client.GetAsync(ForgotPasswordModel.PagePath, TestContext.Current.CancellationToken);
        var signIn = await client.GetStringAsync(new Uri("/account/login", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.DoesNotContain(ForgotPasswordModel.PagePath, signIn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelfService_WithoutEmailDelivery_IsNotOffered()
    {
        factory.EmailSender.IsEnabled = false;

        try
        {
            using var page = await _client.GetAsync(ForgotPasswordModel.PagePath, TestContext.Current.CancellationToken);
            var signIn = await _client.GetStringAsync(new Uri("/account/login", UriKind.Relative), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
            Assert.DoesNotContain(ForgotPasswordModel.PagePath, signIn, StringComparison.Ordinal);
        }
        finally
        {
            factory.EmailSender.IsEnabled = true;
        }
    }

    [Fact]
    public async Task Administrator_SendsResetLink_ThatWorks()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var response = await SendResetAsync(adminToken, userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Sent before the response, so the administrator learns about delivery failures.
        var resetEmail = Assert.Single(factory.EmailSender.SentTo(email));

        Assert.True(await HasPasswordAsync(userId, Password));

        var (_, html) = await ResetAsync(_client, ReadToken(resetEmail), NewPassword, NewPassword);

        Assert.Contains("Your new password is set.", html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, NewPassword));

        var sent = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.PasswordResetSent, userId));

        Assert.Equal(AuditOutcome.Succeeded, sent.Outcome);
        Assert.Equal(administratorId, sent.ActorUserId);
    }

    [Fact]
    public async Task Administrator_LinkWorks_WhileSelfServiceIsDisabled()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:PasswordReset:SelfServiceEnabled", "false"));
        using var client = disabled.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/password-reset"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var (_, html) = await ResetAsync(client, ReadToken(Assert.Single(factory.EmailSender.SentTo(email))), NewPassword, NewPassword);

        Assert.Contains("Your new password is set.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ForgotPasswordModel.PagePath, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_WithoutPermission_IsForbidden()
    {
        var callerId = await factory.CreateUserAsync(PermissionNames.Users.Read);
        var callerToken = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var response = await SendResetAsync(callerToken, userId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.EmailSender.SentTo(email));
    }

    [Fact]
    public async Task Administrator_ForDeactivatedOrUnknownUser_IsRejected()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            (await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Deactivate();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var deactivated = await SendResetAsync(adminToken, userId);
        using var unknown = await SendResetAsync(adminToken, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(factory.EmailSender.SentTo(email));
        Assert.Equal(0, await CountTokensAsync(userId));
    }

    [Fact]
    public async Task Administrator_WithoutEmailDelivery_GetsConflict()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);

        factory.EmailSender.IsEnabled = false;

        try
        {
            using var response = await SendResetAsync(adminToken, userId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(0, await CountTokensAsync(userId));
        }
        finally
        {
            factory.EmailSender.IsEnabled = true;
        }
    }

    [Fact]
    public async Task Administrator_WhenDeliveryFails_GetsBadGateway_WithoutServerDetails()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);

        factory.EmailSender.Fail = true;

        try
        {
            using var response = await SendResetAsync(adminToken, userId);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.DoesNotContain("smtp.example.com", body, StringComparison.Ordinal);
        }
        finally
        {
            factory.EmailSender.Fail = false;
        }

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.PasswordResetSent, userId));

        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
    }

    [Fact]
    public void Link_PointsToTheResetPage()
    {
        var link = PasswordResetLink.Create("https://auth.example.com/base", "a+b/c");

        Assert.Equal("https://auth.example.com/base/account/reset-password?token=a%2Bb%2Fc", link.AbsoluteUri);
    }

    [GeneratedRegex(@"https://\S+/account/reset-password\?token=(?<token>[^\s""&<]+)")]
    private static partial Regex LinkPattern();

    private static string ReadToken(EmailMessage message)
    {
        var match = LinkPattern().Match(message.TextBody);

        Assert.True(match.Success, "The email contains no reset link.");

        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    private static async Task<(HttpStatusCode Status, string Html)> RequestResetAsync(HttpClient client, string email)
    {
        string antiforgeryToken;
        string antiforgeryCookie;

        using (var page = await client.GetAsync(ForgotPasswordModel.PagePath, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);

            (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, ForgotPasswordModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("Email", email),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string> GetResetPageAsync(string token)
    {
        using var page = await _client.GetAsync(ResetPath(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        return await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Opens the link like a browser, then submits the form.
    private static async Task<(HttpStatusCode Status, string Html)> ResetAsync(
        HttpClient client,
        string token,
        string newPassword,
        string confirmPassword)
    {
        using var page = await client.GetAsync(ResetPath(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var antiforgery = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        return await PostAsync(client, token, newPassword, confirmPassword, antiforgery);
    }

    // Submits the form with an antiforgery token of the "Forgot password?" page, without opening the link.
    private static async Task<(HttpStatusCode Status, string Html)> PostResetAsync(
        HttpClient client,
        string token,
        string newPassword,
        string confirmPassword)
    {
        using var page = await client.GetAsync(ForgotPasswordModel.PagePath, TestContext.Current.CancellationToken);

        var antiforgery = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        return await PostAsync(client, token, newPassword, confirmPassword, antiforgery);
    }

    private static async Task<(HttpStatusCode Status, string Html)> PostAsync(
        HttpClient client,
        string token,
        string newPassword,
        string confirmPassword,
        (string Token, string Cookie) antiforgery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PasswordResetLink.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new(PasswordResetLink.TokenParameter, token),
                new("NewPassword", newPassword),
                new("ConfirmPassword", confirmPassword),
                new("__RequestVerificationToken", antiforgery.Token)
            ])
        };
        request.Headers.Add("Cookie", antiforgery.Cookie);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string ResetPath(string token)
        => $"{PasswordResetLink.PagePath}?{PasswordResetLink.TokenParameter}={Uri.EscapeDataString(token)}";

    private async Task<HttpResponseMessage> SendResetAsync(string accessToken, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/password-reset");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpStatusCode> GetPasswordPageStatusAsync(string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PasswordModel.PagePath);
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private async Task<bool> HasPasswordAsync(Guid userId, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        return passwordHasher.Verify((await GetUserAsync(userId)).PasswordHash, password) != PasswordVerification.Failed;
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

    // For events recorded by the background processing of a request at /account/forgot-password.
    private async Task<List<AuditEvent>> WaitForEventsAsync(string type, Guid userId, int count = 1)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (await GetEventsAsync(type, userId) is var events && events.Count < count)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Expected {count} '{type}' events for User {userId}, got {events.Count}.");
            }
        }

        return await GetEventsAsync(type, userId);
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
