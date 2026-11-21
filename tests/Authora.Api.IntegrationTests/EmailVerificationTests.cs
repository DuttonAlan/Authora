// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

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
///     Checks the email verification: the link sent when a User is created or their Email changes, the page it opens,
///     the reset on a change of the address, what administrators can do, and the <c>email_verified</c> claim.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed partial class EmailVerificationTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // Cookies are sent explicitly, so each request uses exactly the Session of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task CreatedUser_GetsLink_AndConfirmingItVerifiesTheEmail()
    {
        var adminToken = await CreateAdministratorTokenAsync();
        var email = NewEmail();

        var userId = await CreateUserAsync(adminToken, email);

        Assert.False((await GetUserJsonAsync(adminToken, userId)).GetProperty("emailVerified").GetBoolean());

        var message = Assert.Single(await factory.EmailSender.WaitForAsync(email));

        Assert.Equal("Confirm your email address", message.Subject);

        var token = ReadToken(message);

        // Opening the link only shows the button, so a mail scanner does not use it up.
        var page = await GetPageAsync(token);

        Assert.Contains("Confirm email address</button>", page, StringComparison.Ordinal);
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);

        var (status, html) = await ConfirmAsync(_client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(VerifyEmailModel.VerifiedMessage, html, StringComparison.Ordinal);

        var user = await GetUserJsonAsync(adminToken, userId);

        Assert.True(user.GetProperty("emailVerified").GetBoolean());
        Assert.Equal(JsonValueKind.String, user.GetProperty("emailVerifiedAt").ValueKind);

        var sent = Assert.Single(await WaitForEventsAsync(AuditEventTypes.Users.EmailVerificationSent, userId));
        var verified = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.EmailVerified, userId));

        Assert.Equal(AuditOutcome.Succeeded, sent.Outcome);
        Assert.Equal("Sent automatically, because the User was created", sent.Details);
        Assert.Equal(AuditOutcome.Succeeded, verified.Outcome);
        Assert.DoesNotContain(token, sent.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsedLink_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var token = await SendLinkAsync(userId, email);

        var (_, first) = await ConfirmAsync(_client, token);

        Assert.Contains(VerifyEmailModel.VerifiedMessage, first, StringComparison.Ordinal);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, await GetPageAsync(token), StringComparison.Ordinal);

        var (status, second) = await PostConfirmAsync(_client, token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, second, StringComparison.Ordinal);

        var events = await GetEventsAsync(AuditEventTypes.Account.EmailVerified, userId);

        Assert.Equal(AuditOutcome.Succeeded, Assert.Single(events).Outcome);
    }

    [Fact]
    public async Task UnknownOrPasswordResetLink_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var resetToken = Guid.NewGuid().ToString("N");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            context.EmailTokens.Add(EmailToken.Create(
                secretHasher.Hash(resetToken), user, EmailTokenPurpose.PasswordReset, DateTime.UtcNow, TimeSpan.FromHours(1)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, await GetPageAsync("not-a-token"), StringComparison.Ordinal);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, await GetPageAsync(new string('a', 500)), StringComparison.Ordinal);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, await GetPageAsync(resetToken), StringComparison.Ordinal);

        var (status, html) = await PostConfirmAsync(_client, resetToken);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, html, StringComparison.Ordinal);
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);
        Assert.Empty(factory.EmailSender.SentTo(email));
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
                secretHasher.Hash(token), user, EmailTokenPurpose.EmailVerification, DateTime.UtcNow.AddHours(-2), TimeSpan.FromHours(1)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (_, html) = await PostConfirmAsync(_client, token);

        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, html, StringComparison.Ordinal);
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);
    }

    [Fact]
    public async Task ChangingTheEmail_ResetsTheVerification_AndSendsANewLink()
    {
        var adminToken = await CreateAdministratorTokenAsync();
        var (userId, oldEmail) = await factory.CreateUserWithPasswordAsync(Password);
        var oldToken = await SendLinkAsync(userId, oldEmail);

        await MarkVerifiedInDatabaseAsync(userId);

        var newEmail = NewEmail();

        using (var response = await UpdateEmailAsync(adminToken, userId, newEmail))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var user = await GetUserJsonAsync(adminToken, userId);

        Assert.False(user.GetProperty("emailVerified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("emailVerifiedAt").ValueKind);

        // The link to the old address no longer works, the one to the new address does.
        var newToken = ReadToken(Assert.Single(await factory.EmailSender.WaitForAsync(newEmail)));

        var (_, rejected) = await PostConfirmAsync(_client, oldToken);

        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, rejected, StringComparison.Ordinal);
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);

        var (_, html) = await ConfirmAsync(_client, newToken);

        Assert.Contains(VerifyEmailModel.VerifiedMessage, html, StringComparison.Ordinal);
        Assert.True((await GetUserAsync(userId)).IsEmailVerified);

        var updated = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.Updated, userId));
        var sent = await WaitForEventsAsync(AuditEventTypes.Users.EmailVerificationSent, userId, 2);

        Assert.Equal("Fields: Email. The new Email is not verified", updated.Details);
        Assert.Contains(sent, auditEvent => auditEvent.Details == "Sent automatically, because the Email was changed");
    }

    [Fact]
    public async Task ChangingOnlyTheCaseOfTheEmail_KeepsTheVerification()
    {
        var adminToken = await CreateAdministratorTokenAsync();
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await MarkVerifiedInDatabaseAsync(userId);

        using (var response = await UpdateEmailAsync(adminToken, userId, email.ToUpperInvariant()))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var user = await GetUserAsync(userId);

        Assert.Equal(email.ToUpperInvariant(), user.Email);
        Assert.True(user.IsEmailVerified);
        Assert.Empty(factory.EmailSender.SentTo(email));
    }

    [Fact]
    public async Task SendAutomatically_CanBeDisabled()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:EmailVerification:SendAutomatically", "false"));
        using var client = disabled.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var adminToken = await CreateAdministratorTokenAsync();
        var email = NewEmail();

        var userId = await CreateUserAsync(adminToken, email, client);

        // An administrator can still send a link.
        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/email-verification"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var sent = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.EmailVerificationSent, userId));

        Assert.Null(sent.Details);
        Assert.Single(factory.EmailSender.SentTo(email));
    }

    [Fact]
    public async Task WithoutEmailDelivery_CreatingAUser_SendsNothing_AndAdministratorGetsConflict()
    {
        var adminToken = await CreateAdministratorTokenAsync();
        var email = NewEmail();

        factory.EmailSender.IsEnabled = false;

        try
        {
            var userId = await CreateUserAsync(adminToken, email);

            using var response = await SendVerificationAsync(adminToken, userId);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(0, await CountTokensAsync(userId));
            Assert.Empty(await GetEventsAsync(AuditEventTypes.Users.EmailVerificationSent, userId));
        }
        finally
        {
            factory.EmailSender.IsEnabled = true;
        }
    }

    [Fact]
    public async Task Administrator_SendsLink_ThatWorks()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var response = await SendVerificationAsync(adminToken, userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Sent before the response, so the administrator learns about delivery failures.
        var (_, html) = await ConfirmAsync(_client, ReadToken(Assert.Single(factory.EmailSender.SentTo(email))));

        Assert.Contains(VerifyEmailModel.VerifiedMessage, html, StringComparison.Ordinal);

        var sent = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.EmailVerificationSent, userId));

        Assert.Equal(AuditOutcome.Succeeded, sent.Outcome);
        Assert.Equal(administratorId, sent.ActorUserId);

        // Once verified, there is nothing to send.
        using var again = await SendVerificationAsync(adminToken, userId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Administrator_ForDeactivatedOrUnknownUser_IsRejected()
    {
        var adminToken = await CreateAdministratorTokenAsync();
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            (await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Deactivate();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var deactivated = await SendVerificationAsync(adminToken, userId);
        using var unknown = await SendVerificationAsync(adminToken, Guid.NewGuid());
        using var unknownMark = await MarkVerifiedAsync(adminToken, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownMark.StatusCode);
        Assert.Empty(factory.EmailSender.SentTo(email));
    }

    [Fact]
    public async Task LinkOfADeactivatedUser_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var token = await SendLinkAsync(userId, email);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            (await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Deactivate();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (_, html) = await PostConfirmAsync(_client, token);

        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, html, StringComparison.Ordinal);
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);
    }

    [Fact]
    public async Task Administrator_MarksEmailVerified_AndOpenLinksStopWorking()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var token = await SendLinkAsync(userId, email);

        using (var response = await MarkVerifiedAsync(adminToken, userId))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var verifiedAt = (await GetUserAsync(userId)).EmailVerifiedAt;

        Assert.NotNull(verifiedAt);
        Assert.Contains(VerifyEmailModel.InvalidLinkMessage, await GetPageAsync(token), StringComparison.Ordinal);

        // A second call changes nothing and records nothing.
        using (var response = await MarkVerifiedAsync(adminToken, userId))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(verifiedAt, (await GetUserAsync(userId)).EmailVerifiedAt);

        var marked = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.EmailMarkedVerified, userId));

        Assert.Equal(administratorId, marked.ActorUserId);
    }

    [Fact]
    public async Task Administrator_WithoutPermission_IsForbidden()
    {
        var callerId = await factory.CreateUserAsync(PermissionNames.Users.Read);
        var callerToken = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var send = await SendVerificationAsync(callerToken, userId);
        using var mark = await MarkVerifiedAsync(callerToken, userId);

        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mark.StatusCode);
        Assert.Empty(factory.EmailSender.SentTo(email));
        Assert.False((await GetUserAsync(userId)).IsEmailVerified);
    }

    [Fact]
    public async Task Administrator_CannotMarkAnAdministratorVerified_WithoutBeingOne()
    {
        var callerId = await factory.CreateUserAsync(PermissionNames.Users.Update);
        var callerToken = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
        var administratorId = await factory.CreateAdministratorAsync();

        using var mark = await MarkVerifiedAsync(callerToken, administratorId);

        Assert.Equal(HttpStatusCode.Forbidden, mark.StatusCode);
        Assert.False((await GetUserAsync(administratorId)).IsEmailVerified);
    }

    [Fact]
    public async Task EmailVerifiedClaim_FollowsTheUser_InUserInfoAndIdToken()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", "email");

        Assert.False((await GetUserInfoAsync(userId)).GetProperty("email_verified").GetBoolean());

        await MarkVerifiedInDatabaseAsync(userId);

        Assert.True((await GetUserInfoAsync(userId)).GetProperty("email_verified").GetBoolean());

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid email"), sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        Assert.True(claims.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public void Link_PointsToTheVerificationPage()
    {
        var link = EmailVerificationLink.Create("https://auth.example.com/base", "a+b/c");

        Assert.Equal("https://auth.example.com/base/account/verify-email?token=a%2Bb%2Fc", link.AbsoluteUri);
    }

    [Theory]
    [InlineData("00:00:30", false)]
    [InlineData("00:01:00", true)]
    [InlineData("1.00:00:00", true)]
    [InlineData("1.00:00:01", false)]
    public void OptionsValidator_LimitsTheLinkLifetime(string lifetime, bool valid)
    {
        var options = new EmailVerificationOptions { LinkLifetime = TimeSpan.Parse(lifetime, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.Equal(valid, new EmailVerificationOptionsValidator().Validate(null, options).Succeeded);
    }

    [GeneratedRegex(@"https://\S+/account/verify-email\?token=(?<token>[^\s""&<]+)")]
    private static partial Regex LinkPattern();

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static string ReadToken(EmailMessage message)
    {
        var match = LinkPattern().Match(message.TextBody);

        Assert.True(match.Success, "The email contains no verification link.");

        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    private async Task<string> CreateAdministratorTokenAsync()
    {
        return await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
    }

    private async Task<Guid> CreateUserAsync(string adminToken, string email, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, firstName = "Jane", lastName = "Doe", password = Password })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var response = await (client ?? _client).SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return created.GetProperty("id").GetGuid();
    }

    // Sends a link through the admin API, which delivers it before it responds.
    private async Task<string> SendLinkAsync(Guid userId, string email)
    {
        using var response = await SendVerificationAsync(await CreateAdministratorTokenAsync(), userId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return ReadToken(factory.EmailSender.SentTo(email)[^1]);
    }

    private async Task<HttpResponseMessage> SendVerificationAsync(string accessToken, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/email-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> MarkVerifiedAsync(string accessToken, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/email-verified");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> UpdateEmailAsync(string accessToken, Guid userId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{userId}")
        {
            Content = JsonContent.Create(new { email })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetUserJsonAsync(string accessToken, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetUserInfoAsync(Guid userId)
    {
        var accessToken = await factory.IssueAccessTokenAsync(userId, ["openid", "email"]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<string> GetPageAsync(string token)
    {
        using var page = await _client.GetAsync(PagePath(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        return await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Opens the link like a browser, then submits the form.
    private static async Task<(HttpStatusCode Status, string Html)> ConfirmAsync(HttpClient client, string token)
    {
        using var page = await client.GetAsync(PagePath(token), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        return await PostAsync(client, token, await AuthorizationCodeFlow.ReadAntiforgeryAsync(page));
    }

    // Submits the form with an antiforgery token of the sign-in page, without opening the link.
    private static async Task<(HttpStatusCode Status, string Html)> PostConfirmAsync(HttpClient client, string token)
    {
        using var page = await client.GetAsync(new Uri("/account/login", UriKind.Relative), TestContext.Current.CancellationToken);

        return await PostAsync(client, token, await AuthorizationCodeFlow.ReadAntiforgeryAsync(page));
    }

    private static async Task<(HttpStatusCode Status, string Html)> PostAsync(
        HttpClient client,
        string token,
        (string Token, string Cookie) antiforgery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EmailVerificationLink.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new(EmailVerificationLink.TokenParameter, token),
                new("__RequestVerificationToken", antiforgery.Token)
            ])
        };
        request.Headers.Add("Cookie", antiforgery.Cookie);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string PagePath(string token)
        => $"{EmailVerificationLink.PagePath}?{EmailVerificationLink.TokenParameter}={Uri.EscapeDataString(token)}";

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

    // For events recorded after an email was queued.
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
