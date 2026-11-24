// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account.ChangePassword;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the page where a signed-in User changes their own password: the current password is required, the
///     password policy applies, and afterwards only the Session of the change stays valid.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ChangePasswordTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private const string NewPassword = "a-brand-new-long-password";

    // Cookies are sent explicitly, so each request uses exactly the Session of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Page_WithoutSession_RedirectsToSignIn()
    {
        using var response = await _client.GetAsync(PasswordModel.PagePath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(SessionAuthenticationHandler.SignInPath, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_SetsNewPassword_KeepsThisSession_AndRevokesTheOthers()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var currentSession = await factory.CreateSessionAsync(userId);
        var otherSession = await factory.CreateSessionAsync(userId);
        var accessToken = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        using (var before = await GetUserInfoAsync(accessToken))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        var (status, html) = await ChangeAsync(_client, currentSession, Password, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Your password was changed.", html, StringComparison.Ordinal);

        Assert.True(await HasPasswordAsync(userId, NewPassword));
        Assert.False(await HasPasswordAsync(userId, Password));

        // This browser stays signed in, every other Session and every token ends.
        Assert.Equal(HttpStatusCode.OK, await GetPageStatusAsync(currentSession));
        Assert.Equal(HttpStatusCode.Redirect, await GetPageStatusAsync(otherSession));

        using (var after = await GetUserInfoAsync(accessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        }

        var events = await GetEventsAsync(userId);
        var changed = Assert.Single(events);

        Assert.Equal(AuditOutcome.Succeeded, changed.Outcome);
        Assert.Equal(userId, changed.ActorUserId);
        Assert.DoesNotContain(Password, changed.Details ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(NewPassword, changed.Details ?? string.Empty, StringComparison.Ordinal);

        var notification = Assert.Single(factory.EmailSender.SentTo(email));

        Assert.Equal("Your password was changed", notification.Subject);
        Assert.DoesNotContain(NewPassword, notification.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_WithWrongCurrentPassword_KeepsPasswordAndSessions_AndCountsTheAttempt()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var currentSession = await factory.CreateSessionAsync(userId);
        var otherSession = await factory.CreateSessionAsync(userId);

        var (status, html) = await ChangeAsync(_client, currentSession, "not-the-current-password", NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(PasswordModel.WrongPasswordMessage, html, StringComparison.Ordinal);

        Assert.True(await HasPasswordAsync(userId, Password));
        Assert.Equal(HttpStatusCode.OK, await GetPageStatusAsync(otherSession));
        Assert.Empty(factory.EmailSender.SentTo(email));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var user = await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        Assert.Equal(1, user.FailedLoginCount);

        var failed = Assert.Single(await GetEventsAsync(userId));

        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.StartsWith("Wrong current password", failed.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_WhileLockedOut_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            // One failed attempt with a limit of one locks the User out.
            await users.RecordFailedSignIn(userId, DateTime.UtcNow, 1, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        }

        var (status, html) = await ChangeAsync(_client, session, Password, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(PasswordModel.LockedOutMessage, html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, Password));
    }

    [Theory]
    [InlineData("too-short", "too-short", "The new password must be 12 to 128 characters long.")]
    [InlineData(NewPassword, "a-different-new-password", "The new passwords do not match.")]
    public async Task Change_ViolatingThePolicy_KeepsPassword(string newPassword, string confirmPassword, string message)
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        var (status, html) = await ChangeAsync(_client, session, Password, newPassword, confirmPassword);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.True(await HasPasswordAsync(userId, Password));
        Assert.Empty(await GetEventsAsync(userId));
    }

    [Fact]
    public async Task Validation_OfThisSession_DuringTheChange_DoesNotRevokeIt()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        // A concurrent request of the same browser: it loaded the Session before the change committed, and loads the
        // User after it, so it sees the old Session stamp and the new User stamp.
        await using var concurrentRequest = factory.Services.CreateAsyncScope();
        var secretHasher = concurrentRequest.ServiceProvider.GetRequiredService<ISecretHasher>();
        var sessions = concurrentRequest.ServiceProvider.GetRequiredService<ISessionRepository>();
        var staleSession = await sessions.GetSessionByTokenHash(secretHasher.Hash(sessionToken), TestContext.Current.CancellationToken);

        Assert.NotNull(staleSession);

        await using (var change = factory.Services.CreateAsyncScope())
        {
            var result = await change.ServiceProvider.GetRequiredService<ChangePasswordService>().ExecuteAsync(
                userId,
                staleSession.Id,
                new ChangePasswordRequest(Password, NewPassword),
                TestContext.Current.CancellationToken);

            Assert.Equal(ChangePasswordResult.Changed, result);
        }

        var validated = await concurrentRequest.ServiceProvider.GetRequiredService<ValidateSessionService>()
            .ExecuteAsync(sessionToken, TestContext.Current.CancellationToken);

        Assert.Equal(staleSession.Id, validated?.SessionId);
        Assert.Equal(HttpStatusCode.OK, await GetPageStatusAsync(sessionToken));
    }

    [Fact]
    public async Task Change_IsRateLimitedLikeSignIn()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:SignInPermitsPerMinute", "1"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var session = await factory.CreateSessionAsync(userId);

        var (first, _) = await ChangeAsync(client, session, "not-the-current-password", NewPassword, NewPassword);
        var (second, _) = await ChangeAsync(client, session, Password, NewPassword, NewPassword);

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.TooManyRequests, second);
        Assert.True(await HasPasswordAsync(userId, Password));
    }

    private static async Task<(HttpStatusCode Status, string Html)> ChangeAsync(
        HttpClient client,
        string sessionToken,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        string antiforgeryToken;
        string antiforgeryCookie;

        using (var pageRequest = new HttpRequestMessage(HttpMethod.Get, PasswordModel.PagePath))
        {
            pageRequest.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

            using var page = await client.SendAsync(pageRequest, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);

            (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, PasswordModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("CurrentPassword", currentPassword),
                new("NewPassword", newPassword),
                new("ConfirmPassword", confirmPassword),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<HttpStatusCode> GetPageStatusAsync(string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, PasswordModel.PagePath);
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> GetUserInfoAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<bool> HasPasswordAsync(Guid userId, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        return passwordHasher.Verify(user.PasswordHash!, password) != PasswordVerification.Failed;
    }

    private async Task<List<Core.Domain.Entities.AuditEvent>> GetEventsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == AuditEventTypes.Account.PasswordChanged && auditEvent.TargetId == userId.ToString())
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
