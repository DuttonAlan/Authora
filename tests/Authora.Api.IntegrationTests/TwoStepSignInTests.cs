// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

using Authora.Api.Authentication;
using Authora.Api.Authorization;
using Authora.Api.Pages.Account;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the two-step sign-in: Users with a second factor get only a pending sign-in for the password, which is
///     short-lived, single-use and bound to their Security Stamp; Sessions record how the User authenticated; step-up
///     and recent authentication.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed partial class TwoStepSignInTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private const string Code = "123456";

    // Cookies are sent explicitly, so each request uses exactly the cookies of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task SignIn_WithoutSecondFactor_StartsSessionWithPassword()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var response = await SignInAsync(email, Password, SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SecurityModel.PagePath, response.Headers.Location?.OriginalString);
        Assert.NotNull(ReadCookie(response, SessionCookie.Name));
        Assert.Null(ReadCookie(response, PendingSignInCookie.Name));

        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal("pwd", session.AuthenticationMethod);
        Assert.Equal(session.CreatedAt, session.AuthenticatedAt);
        Assert.Empty(await GetChallengesAsync(userId));
    }

    [Fact]
    public async Task SignIn_WithSecondFactor_GivesNoSessionCookie_OnlyAPendingSignIn()
    {
        var (userId, email) = await CreateEnrolledUserAsync();

        using var response = await SignInAsync(email, Password, SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"{SecondFactorModel.PagePath}?returnUrl={Uri.EscapeDataString(SecurityModel.PagePath)}",
            response.Headers.Location?.OriginalString);
        Assert.Null(ReadCookie(response, SessionCookie.Name));
        Assert.NotNull(ReadCookie(response, PendingSignInCookie.Name));

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith($"{PendingSignInCookie.Name}=", StringComparison.Ordinal))
            .ToLowerInvariant();
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
        Assert.Contains("max-age=300", cookie, StringComparison.Ordinal);

        Assert.Empty(await GetSessionsAsync(userId));

        var challenge = Assert.Single(await GetChallengesAsync(userId));
        Assert.Equal("pwd", challenge.AuthenticationMethod);
        Assert.Null(challenge.SessionId);
        Assert.Null(challenge.ConsumedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), challenge.ExpiresAt - challenge.CreatedAt);

        var pending = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.SignInPending, userId));
        Assert.Equal($"Pending sign-in {challenge.Id}", pending.Details);
        Assert.Empty(await GetEventsAsync(AuditEventTypes.Account.SignIn, userId));
    }

    [Fact]
    public async Task SignIn_WithSecondFactor_AndWrongPassword_StartsNoPendingSignIn()
    {
        var (userId, email) = await CreateEnrolledUserAsync();

        using var response = await SignInAsync(email, "a-wrong-password-123", SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(ReadCookie(response, PendingSignInCookie.Name));
        Assert.Empty(await GetChallengesAsync(userId));
    }

    [Fact]
    public async Task SecondFactorPage_ShowsTheFactor()
    {
        var (_, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        using var request = new HttpRequestMessage(HttpMethod.Get, SecondFactorModel.PagePath);
        request.Headers.Add("Cookie", $"{PendingSignInCookie.Name}={pendingToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Test authenticator", html, StringComparison.Ordinal);
        Assert.Contains(email, html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"one-time-code\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondFactorPage_WithoutPendingSignIn_AsksToSignInAgain()
    {
        using var response = await _client.GetAsync(SecondFactorModel.PagePath, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in again", html, StringComparison.Ordinal);
        Assert.DoesNotContain("one-time-code", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondFactor_WithCorrectCode_StartsSessionWithBothMethods()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        using var response = await SubmitCodeAsync(pendingToken, Code, returnUrl: SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SecurityModel.PagePath, response.Headers.Location?.OriginalString);
        Assert.True(IsDeleted(response, PendingSignInCookie.Name));

        var sessionToken = ReadCookie(response, SessionCookie.Name);
        Assert.NotNull(sessionToken);

        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal("pwd otp", session.AuthenticationMethod);
        Assert.NotNull(Assert.Single(await GetChallengesAsync(userId)).ConsumedAt);

        var signIn = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.SignIn, userId));
        Assert.Equal(AuditOutcome.Succeeded, signIn.Outcome);
        Assert.StartsWith($"Session {session.Id} (pwd otp)", signIn.Details, StringComparison.Ordinal);

        // The Session works and the overview shows how the User signed in.
        var html = await GetPageAsync(SecurityModel.PagePath, sessionToken);
        Assert.Contains("Password and Test authenticator", html, StringComparison.Ordinal);
        Assert.Contains("Test authenticator: on", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SecondFactor_WithWrongCode_CountsTowardsLockout_AndCanBeRetried()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        using (var wrong = await SubmitCodeAsync(pendingToken, "654321"))
        {
            var html = await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains(SecondFactorModel.WrongResponseMessage, html, StringComparison.Ordinal);
            Assert.Null(ReadCookie(wrong, SessionCookie.Name));
        }

        Assert.Equal(1, (await GetUserAsync(userId)).FailedLoginCount);

        var failure = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.SignIn, userId));
        Assert.Equal(AuditOutcome.Failed, failure.Outcome);
        Assert.StartsWith("Wrong second factor (otp)", failure.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("654321", failure.Details, StringComparison.Ordinal);

        using (var correct = await SubmitCodeAsync(pendingToken, Code))
        {
            Assert.Equal(HttpStatusCode.Redirect, correct.StatusCode);
            Assert.NotNull(ReadCookie(correct, SessionCookie.Name));
        }

        // Only a complete sign-in resets the failed attempts.
        Assert.Equal(0, (await GetUserAsync(userId)).FailedLoginCount);
    }

    [Fact]
    public async Task SecondFactor_TooManyWrongCodes_LockTheUserOut()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);
        string html = string.Empty;

        for (var attempt = 0; attempt < new SignInOptions().MaxFailedAttempts; attempt++)
        {
            using var wrong = await SubmitCodeAsync(pendingToken, "000000");
            html = await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        Assert.Contains(SecondFactorModel.LockedOutMessage, html, StringComparison.Ordinal);
        Assert.True((await GetUserAsync(userId)).LockoutEndAt > DateTime.UtcNow);

        // Locked out: not even the correct code signs in.
        using var correct = await SubmitCodeAsync(pendingToken, Code);

        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        Assert.Null(ReadCookie(correct, SessionCookie.Name));
        Assert.Empty(await GetSessionsAsync(userId));
    }

    [Fact]
    public async Task SecondFactor_IsSingleUse()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        using (var first = await SubmitCodeAsync(pendingToken, Code))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        using var second = await SubmitCodeAsync(pendingToken, Code);
        var html = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("Sign in again", html, StringComparison.Ordinal);
        Assert.Null(ReadCookie(second, SessionCookie.Name));
        Assert.Single(await GetSessionsAsync(userId));
    }

    [Fact]
    public async Task SecondFactor_ConcurrentRequests_StartOnlyOneSession()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);
        var antiforgery = await GetAntiforgeryAsync(sessionToken: null);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PostCodeAsync(pendingToken, Code, null, null, antiforgery)));

        try
        {
            Assert.Single(responses, response => ReadCookie(response, SessionCookie.Name) is not null);
            Assert.Single(await GetSessionsAsync(userId));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task SecondFactor_AfterExpiry_IsRejected()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            await context.SignInChallenges
                .Where(challenge => challenge.UserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(challenge => challenge.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)),
                    TestContext.Current.CancellationToken);
        }

        using var response = await SubmitCodeAsync(pendingToken, Code);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in again", html, StringComparison.Ordinal);
        Assert.True(IsDeleted(response, PendingSignInCookie.Name));
        Assert.Null(ReadCookie(response, SessionCookie.Name));
        Assert.Empty(await GetSessionsAsync(userId));

        // An expired pending sign-in is not a wrong code.
        Assert.Equal(0, (await GetUserAsync(userId)).FailedLoginCount);
    }

    [Fact]
    public async Task SecondFactor_AfterSecurityStampChanged_IsRejected()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        // E.g. an administrator signed the User out everywhere, or the password was reset meanwhile.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            user.RotateSecurityStamp();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await SubmitCodeAsync(pendingToken, Code);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in again", html, StringComparison.Ordinal);
        Assert.Null(ReadCookie(response, SessionCookie.Name));
        Assert.Empty(await GetSessionsAsync(userId));
    }

    [Fact]
    public async Task SecondFactor_ThatExpiresWhileVerifying_IsRejected()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var pendingToken = await StartSignInAsync(email);

        // A slow verification: the pending sign-in expires after it was loaded, but before it is completed.
        factory.SecondFactor.WhileVerifying(userId, async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            await context.SignInChallenges
                .Where(challenge => challenge.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ExpiresAt, DateTime.UtcNow));
        });

        using var response = await SubmitCodeAsync(pendingToken, Code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(ReadCookie(response, SessionCookie.Name));
        Assert.Empty(await GetSessionsAsync(userId));
        Assert.Null(Assert.Single(await GetChallengesAsync(userId)).ConsumedAt);
    }

    [Fact]
    public async Task StepUp_OfASessionWithAnOutdatedToken_CannotBeSaved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.CreateSessionAsync(userId);
        var sessionId = Assert.Single(await GetSessionsAsync(userId)).Id;

        await using var outdatedScope = factory.Services.CreateAsyncScope();
        var outdatedRepository = outdatedScope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var secretHasher = outdatedScope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var outdated = (await outdatedRepository.GetSessionById(sessionId, cancellationToken))!;

        // Another request steps the Session up after this one loaded it.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var session = (await repository.GetSessionById(sessionId, cancellationToken))!;

            session.RecordStepUp(secretHasher.Hash("token-of-the-first-step-up"), FakeSecondFactor.MethodName, DateTime.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
        }

        outdated.RecordStepUp(secretHasher.Hash("token-of-the-second-step-up"), "hwk", DateTime.UtcNow);

        await Assert.ThrowsAsync<ConflictException>(() => outdatedRepository.SaveChangesAsync(cancellationToken));

        var stored = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal("pwd otp", stored.AuthenticationMethod);
        Assert.Equal(secretHasher.Hash("token-of-the-first-step-up"), stored.TokenHash);
    }

    [Fact]
    public async Task SessionWithoutMethods_FromBeforeTheUpgrade_HasEnded_AlsoForItsCodes()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var sessionToken = await factory.CreateSessionAsync(userId);
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        // What the migration leaves in Sessions that existed before it.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            await context.Sessions
                .Where(session => session.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.AuthenticationMethod, string.Empty), TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, sessionToken));

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", AuthorizationCodeFlow.CodeVerifier)
        ]);
        using var exchange = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
        var error = await exchange.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, exchange.StatusCode);
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SecondFactor_IsRateLimitedLikeSignIn()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:SignInPermitsPerMinute", "1"));
        using var client = limited.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (_, email) = await CreateEnrolledUserAsync();

        using var signIn = await SignInAsync(email, Password, returnUrl: null, client);
        var pendingToken = ReadCookie(signIn, PendingSignInCookie.Name)!;

        using var code = await PostCodeAsync(pendingToken, Code, null, null, await GetAntiforgeryAsync(null, client), client);

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, code.StatusCode);
    }

    [Fact]
    public async Task AuthorizationCode_CarriesTheMethodsAndTheTimeOfTheSession()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var pendingToken = await StartSignInAsync(email);

        string sessionToken;

        using (var signedIn = await SubmitCodeAsync(pendingToken, Code))
        {
            sessionToken = ReadCookie(signedIn, SessionCookie.Name)!;
        }

        var session = Assert.Single(await GetSessionsAsync(userId));
        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client,
            AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"),
            sessionToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var authorizationCode = await context.AuthorizationCodes.AsNoTracking()
                .SingleAsync(authorizationCode => authorizationCode.UserId == userId, TestContext.Current.CancellationToken);

            Assert.Equal("pwd otp", authorizationCode.AuthenticationMethod);
            Assert.Equal(session.Id, authorizationCode.SessionId);
        }

        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        Assert.Equal(new DateTimeOffset(session.AuthenticatedAt, TimeSpan.Zero).ToUnixTimeSeconds(), claims.GetProperty("auth_time").GetInt64());
    }

    [Fact]
    public async Task StepUp_AddsTheFactorToTheSession_WithANewToken()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var signedInAt = DateTime.UtcNow.AddMinutes(-30);
        var sessionToken = await factory.CreateSessionAsync(userId, signedInAt);

        string pendingToken;

        using (var start = await GetAsync($"{StepUpModel.PagePath}?returnUrl={Uri.EscapeDataString(ConsentsModel.PagePath)}", sessionToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            Assert.Equal(
                $"{SecondFactorModel.PagePath}?returnUrl={Uri.EscapeDataString(ConsentsModel.PagePath)}",
                start.Headers.Location?.OriginalString);

            pendingToken = ReadCookie(start, PendingSignInCookie.Name)!;
            Assert.NotNull(pendingToken);
        }

        var before = Assert.Single(await GetSessionsAsync(userId));
        var challenge = Assert.Single(await GetChallengesAsync(userId));
        Assert.Equal(before.Id, challenge.SessionId);
        Assert.Equal("pwd", challenge.AuthenticationMethod);

        using var response = await SubmitCodeAsync(pendingToken, Code, sessionToken, ConsentsModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ConsentsModel.PagePath, response.Headers.Location?.OriginalString);

        var newToken = ReadCookie(response, SessionCookie.Name);
        Assert.NotNull(newToken);
        Assert.NotEqual(sessionToken, newToken);

        // The same Session, with both methods, authenticated just now; the old token no longer works.
        var after = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal(before.Id, after.Id);
        Assert.Equal("pwd otp", after.AuthenticationMethod);
        Assert.True(after.AuthenticatedAt > signedInAt.AddMinutes(29));
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);

        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, sessionToken));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(SecurityModel.PagePath, newToken));

        var stepUp = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.StepUp, userId));
        Assert.Equal(AuditOutcome.Succeeded, stepUp.Outcome);
        Assert.Equal($"Session {after.Id} (pwd otp)", stepUp.Details);
    }

    [Fact]
    public async Task StepUp_InAnotherSession_IsRejected()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var otherSessionToken = await factory.CreateSessionAsync(userId);

        string pendingToken;

        using (var start = await GetAsync(StepUpModel.PagePath, sessionToken))
        {
            pendingToken = ReadCookie(start, PendingSignInCookie.Name)!;
        }

        using var response = await SubmitCodeAsync(pendingToken, Code, otherSessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(ReadCookie(response, SessionCookie.Name));
        Assert.All(await GetSessionsAsync(userId), session => Assert.Equal("pwd", session.AuthenticationMethod));
    }

    [Fact]
    public async Task StepUp_WithoutMissingFactor_HasNothingToConfirm()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var response = await GetAsync(StepUpModel.PagePath, sessionToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nothing to confirm", html, StringComparison.Ordinal);
        Assert.Null(ReadCookie(response, PendingSignInCookie.Name));
        Assert.Empty(await GetChallengesAsync(userId));
    }

    [Fact]
    public async Task PasswordReset_DoesNotSignIn_AndDoesNotSkipTheSecondFactor()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        const string NewPassword = "a-brand-new-long-password";

        using (var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/password-reset"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            using var sent = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.True(sent.IsSuccessStatusCode);
        }

        var message = Assert.Single(await factory.EmailSender.WaitForAsync(email));
        var token = Uri.UnescapeDataString(ResetLinkPattern().Match(message.TextBody).Groups["token"].Value);
        var resetPath = $"/account/reset-password?token={Uri.EscapeDataString(token)}";

        (string Token, string Cookie) antiforgery;

        using (var page = await _client.GetAsync(resetPath, TestContext.Current.CancellationToken))
        {
            antiforgery = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
        }

        using (var reset = new HttpRequestMessage(HttpMethod.Post, "/account/reset-password"))
        {
            reset.Content = new FormUrlEncodedContent(
            [
                new("token", token),
                new("NewPassword", NewPassword),
                new("ConfirmPassword", NewPassword),
                new("__RequestVerificationToken", antiforgery.Token)
            ]);
            reset.Headers.Add("Cookie", antiforgery.Cookie);

            using var response = await _client.SendAsync(reset, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(ReadCookie(response, SessionCookie.Name));
            Assert.Null(ReadCookie(response, PendingSignInCookie.Name));
        }

        Assert.Empty(await GetSessionsAsync(userId));

        // The new password still needs the second factor.
        using var signIn = await SignInAsync(email, NewPassword, returnUrl: null);

        Assert.Null(ReadCookie(signIn, SessionCookie.Name));
        Assert.NotNull(ReadCookie(signIn, PendingSignInCookie.Name));
    }

    [Fact]
    public async Task Cleanup_DeletesExpiredPendingSignIns()
    {
        var (userId, email) = await CreateEnrolledUserAsync();
        await StartSignInAsync(email);
        await StartSignInAsync(email);

        Guid expiredId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            expiredId = await context.SignInChallenges
                .Where(challenge => challenge.UserId == userId)
                .OrderBy(challenge => challenge.CreatedAt)
                .Select(challenge => challenge.Id)
                .FirstAsync(TestContext.Current.CancellationToken);

            await context.SignInChallenges
                .Where(challenge => challenge.Id == expiredId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(challenge => challenge.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)),
                    TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>().CleanAsync(DateTime.UtcNow, TestContext.Current.CancellationToken);
        }

        var remaining = await GetChallengesAsync(userId);

        Assert.Single(remaining);
        Assert.DoesNotContain(remaining, challenge => challenge.Id == expiredId);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-9, true)]
    [InlineData(-11, false)]
    public async Task RecentAuthenticationPolicy_RequiresAuthenticationWithinTheWindow(int minutesAgo, bool succeeds)
    {
        var authenticatedAt = DateTimeOffset.UtcNow.AddMinutes(minutesAgo).ToUnixTimeSeconds();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(SessionClaimTypes.AuthenticationTime, authenticatedAt.ToString(System.Globalization.CultureInfo.InvariantCulture))],
            SessionAuthenticationHandler.SchemeName));

        await using var scope = factory.Services.CreateAsyncScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var result = await authorizationService.AuthorizeAsync(user, AuthorizationPolicies.RecentAuthentication);

        Assert.Equal(succeeds, result.Succeeded);
    }

    [Theory]
    [InlineData("00:00:00", "00:10:00")]
    [InlineData("00:16:00", "00:10:00")]
    [InlineData("00:05:00", "00:00:00")]
    [InlineData("00:05:00", "02:00:00")]
    public void Options_RejectInvalidDurations(string pendingSignInLifetime, string recentAuthenticationWindow)
    {
        var options = new SignInOptions
        {
            PendingSignInLifetime = TimeSpan.Parse(pendingSignInLifetime, System.Globalization.CultureInfo.InvariantCulture),
            RecentAuthenticationWindow = TimeSpan.Parse(recentAuthenticationWindow, System.Globalization.CultureInfo.InvariantCulture)
        };

        Assert.True(new SignInOptionsValidator().Validate(null, options).Failed);
        Assert.True(new SignInOptionsValidator().Validate(null, new SignInOptions()).Succeeded);
    }

    [Fact]
    public async Task SecurityPage_WithoutSession_RedirectsToSignIn()
    {
        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, sessionToken: null));
    }

    [GeneratedRegex(@"https://\S+/account/reset-password\?token=(?<token>[^\s""&<]+)")]
    private static partial Regex ResetLinkPattern();

    private async Task<(Guid Id, string Email)> CreateEnrolledUserAsync()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        factory.SecondFactor.Enroll(userId, Code);

        return (userId, email);
    }

    // Signs in with the password and returns the token of the pending sign-in.
    private async Task<string> StartSignInAsync(string email)
    {
        using var response = await SignInAsync(email, Password, returnUrl: null);

        return ReadCookie(response, PendingSignInCookie.Name) ?? throw new InvalidOperationException("No pending sign-in was started.");
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password, string? returnUrl, HttpClient? client = null)
    {
        client ??= _client;

        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(sessionToken: null, client);

        var form = new List<KeyValuePair<string, string>>
        {
            new("Email", email),
            new("Password", password),
            new("__RequestVerificationToken", antiforgeryToken)
        };

        if (returnUrl is not null)
        {
            form.Add(new("ReturnUrl", returnUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionAuthenticationHandler.SignInPath)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SubmitCodeAsync(string pendingToken, string code, string? sessionToken = null, string? returnUrl = null)
    {
        return await PostCodeAsync(pendingToken, code, sessionToken, returnUrl, await GetAntiforgeryAsync(sessionToken));
    }

    private async Task<HttpResponseMessage> PostCodeAsync(
        string pendingToken,
        string code,
        string? sessionToken,
        string? returnUrl,
        (string Token, string Cookie) antiforgery,
        HttpClient? client = null)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("Method", FakeSecondFactor.MethodName),
            new("Code", code),
            new("__RequestVerificationToken", antiforgery.Token)
        };

        if (returnUrl is not null)
        {
            form.Add(new("ReturnUrl", returnUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SecondFactorModel.PagePath)
        {
            Content = new FormUrlEncodedContent(form)
        };

        var cookies = $"{PendingSignInCookie.Name}={pendingToken}; {antiforgery.Cookie}";
        request.Headers.Add("Cookie", sessionToken is null ? cookies : $"{cookies}; {SessionCookie.Name}={sessionToken}");

        return await (client ?? _client).SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The antiforgery token is bound to the signed-in User, so it is read with the Session cookie the form is sent with.
    // The sign-in page with a ReturnUrl always shows a form, also to a signed-in User.
    private async Task<(string Token, string Cookie)> GetAntiforgeryAsync(string? sessionToken, HttpClient? client = null)
    {
        using var page = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2F", sessionToken, client);

        return await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await (client ?? _client).SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpStatusCode> GetStatusAsync(string path, string? sessionToken)
    {
        using var response = await GetAsync(path, sessionToken);

        return response.StatusCode;
    }

    private async Task<string> GetPageAsync(string path, string sessionToken)
    {
        using var response = await GetAsync(path, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The value of a cookie the response sets, or null if it sets none or deletes it.
    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var value = cookies
            .Select(cookie => cookie.Split(';')[0])
            .Where(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal))
            .Select(cookie => cookie[(name.Length + 1)..])
            .LastOrDefault();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static bool IsDeleted(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith($"{name}=;", StringComparison.Ordinal));
    }

    private async Task<List<Session>> GetSessionsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Sessions.AsNoTracking()
            .Where(session => session.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SignInChallenge>> GetChallengesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.SignInChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<User> GetUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetEventsAsync(string type, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var target = userId.ToString();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == target)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
