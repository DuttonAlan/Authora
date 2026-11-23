// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account.ChangePassword;
using Authora.Core.Application.Account.ResetPassword;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.BackchannelLogout;
using Authora.Core.Application.Clients.Get;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks OpenID Connect Back-Channel Logout 1.0: the <c>sid</c> claim, the Client settings, that every path that
///     ends Sessions revokes their Grants and queues a notification per Client, and the delivery of the Logout Tokens.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class BackchannelLogoutTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct horse battery staple";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Discovery_AdvertisesBackchannelLogoutWithSid()
    {
        var discovery = await _client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        Assert.True(discovery.GetProperty("backchannel_logout_supported").GetBoolean());
        Assert.True(discovery.GetProperty("backchannel_logout_session_supported").GetBoolean());
        Assert.Contains("sid", discovery.GetProperty("claims_supported").EnumerateArray().Select(claim => claim.GetString()));
    }

    [Fact]
    public async Task SignOutWithIdTokenHint_RevokesTokensAndSendsLogoutTokenWithSidOfIdToken()
    {
        var (userId, sessionToken) = await SignInAsync();
        var (clientId, redirectUri, logoutUri) = await CreateClientAsync();

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var idToken = tokens.GetProperty("id_token").GetString()!;
        var (_, idClaims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, idToken);
        var sid = idClaims.GetProperty("sid").GetString();

        Assert.Equal((await GetSessionAsync(userId)).Sid, sid);

        using (var logout = await LogoutAsync(sessionToken, idToken))
        {
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        }

        // The Access Token of the Session no longer works.
        using (var userInfo = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo"))
        {
            userInfo.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());

            using var response = await _client.SendAsync(userInfo, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await factory.DeliverBackchannelLogoutsAsync();

        var received = Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(logoutUri));
        Assert.Equal(HttpMethod.Post, received.Method);
        Assert.Equal("application/x-www-form-urlencoded", received.MediaType);

        var (header, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, received.LogoutToken!);
        Assert.Equal("logout+jwt", header.GetProperty("typ").GetString());
        Assert.Equal(AuthoraApiFactory.Issuer, claims.GetProperty("iss").GetString());
        Assert.Equal(clientId.ToString(), claims.GetProperty("aud").GetString());
        Assert.Equal(userId.ToString(), claims.GetProperty("sub").GetString());
        Assert.Equal(sid, claims.GetProperty("sid").GetString());
        Assert.True(Guid.TryParse(claims.GetProperty("jti").GetString(), out _));
        Assert.False(claims.TryGetProperty("nonce", out _));

        var iat = claims.GetProperty("iat").GetInt64();
        var exp = claims.GetProperty("exp").GetInt64();
        Assert.InRange(exp - iat, 1, (long)LogoutTokenIssuer.Lifetime.TotalSeconds);

        var events = claims.GetProperty("events");
        Assert.Equal(JsonValueKind.Object, events.GetProperty(LogoutTokenIssuer.BackchannelLogoutEvent).ValueKind);
        Assert.Single(events.EnumerateObject());

        // Delivered, so it is not sent again.
        Assert.Empty(await GetNotificationsAsync(clientId));
    }

    [Fact]
    public async Task AdminRevokeSessions_RevokesGrantsAndNotifiesOnlyClientsWithUri()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/users/{setup.UserId}/revoke-sessions", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task AdminDeactivate_RevokesGrantsAndNotifies()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/users/{setup.UserId}/deactivate", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task AdminDelete_RevokesGrantsAndNotifies()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{setup.UserId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task AdminSetPassword_RevokesGrantsAndNotifies()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{setup.UserId}/password", token, new { Password = "another correct horse battery" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task AdminResetMfa_RevokesGrantsAndNotifies()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        await factory.EnrollTotpAsync(setup.UserId);
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{setup.UserId}/mfa", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task PasswordReset_RevokesGrantsAndNotifies()
    {
        var setup = await SetUpSessionWithGrantsAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var user = await context.Users.SingleAsync(user => user.Id == setup.UserId, TestContext.Current.CancellationToken);
            var link = await scope.ServiceProvider.GetRequiredService<Core.Application.Email.EmailTokenIssuer>()
                .IssueAsync(user, EmailTokenPurpose.PasswordReset, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

            var result = await scope.ServiceProvider.GetRequiredService<ResetPasswordService>()
                .ExecuteAsync(new ResetPasswordRequest(link, "another correct horse battery"), TestContext.Current.CancellationToken);

            Assert.Equal(ResetPasswordResult.Reset, result);
        }

        await AssertEndedAndNotifiedAsync(setup);
    }

    [Fact]
    public async Task PasswordChange_EndsOtherSessionsOnly()
    {
        var setup = await SetUpSessionWithGrantsAsync(password: Password);

        // The User changes the password in a second Session, which stays valid without a notification.
        await factory.CreateSessionAsync(setup.UserId);
        var current = (await GetSessionsAsync(setup.UserId)).Single(session => session.Id != setup.SessionId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ChangePasswordService>().ExecuteAsync(
                setup.UserId,
                current.Id,
                new ChangePasswordRequest(Password, "another correct horse battery"),
                TestContext.Current.CancellationToken);

            Assert.Equal(ChangePasswordResult.Changed, result);
        }

        await AssertEndedAndNotifiedAsync(setup);
        Assert.Null((await GetSessionsAsync(setup.UserId)).Single(session => session.Id == current.Id).RevokedAt);
    }

    [Fact]
    public async Task SignOutAtTheSameTime_NotifiesOnce()
    {
        var setup = await SetUpSessionWithGrantsAsync();

        // Like /account/logout in two tabs at once.
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SignOutService>().ExecuteAsync(setup.SessionToken, TestContext.Current.CancellationToken);
        }));

        await AssertEndedAndNotifiedAsync(setup);
        Assert.Equal(
            [AuditEventTypes.Account.SignedOut],
            await GetAuditTypesAsync(setup.UserId, AuditEventTypes.Account.SignedOut));
    }

    [Fact]
    public async Task CodeOfEndedSession_IsRejectedAndCreatesNoGrant()
    {
        var (userId, sessionToken) = await SignInAsync();
        var (clientId, redirectUri, _) = await CreateClientAsync();
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        using (await LogoutAsync(sessionToken, idTokenHint: null, confirm: true))
        {
        }

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", AuthorizationCodeFlow.CodeVerifier)
        ]);

        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        Assert.False(await context.Grants.AnyAsync(grant => grant.UserId == userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateClient_SetsAndRemovesBackchannelLogoutUri()
    {
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");
        var token = await IssueAdminTokenAsync();
        var uri = $"https://{Guid.NewGuid():N}.example.com/backchannel-logout?tenant=1";

        using (var response = await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}", token, new { BackchannelLogoutUri = uri, BackchannelLogoutSessionRequired = true }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var client = await GetClientAsync(clientId, token);
        Assert.Equal(uri, client.BackchannelLogoutUri);
        Assert.True(client.BackchannelLogoutSessionRequired);

        // Other changes leave it as it is.
        using (var response = await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}", token, new { Name = "Renamed" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(uri, (await GetClientAsync(clientId, token)).BackchannelLogoutUri);

        using (var response = await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}", token, new { BackchannelLogoutUri = "" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Null((await GetClientAsync(clientId, token)).BackchannelLogoutUri);
    }

    [Theory]
    [InlineData("http://app.example.com/logout")]
    [InlineData("https://app.example.com/logout#fragment")]
    [InlineData("/logout")]
    [InlineData("https://user:secret@app.example.com/logout")]
    public async Task UpdateClient_InvalidBackchannelLogoutUri_IsRejected(string uri)
    {
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");
        var token = await IssueAdminTokenAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}", token, new { BackchannelLogoutUri = uri });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await GetClientAsync(clientId, token)).BackchannelLogoutUri);
    }

    [Fact]
    public async Task FailedDelivery_IsRetriedWithBackoff()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        factory.BackchannelLogoutHandler.Respond(setup.LogoutUri, HttpStatusCode.InternalServerError);

        await EndSessionAsync(setup);
        var now = DateTime.UtcNow;
        await factory.DeliverBackchannelLogoutsAsync(now);

        var notification = Assert.Single(await GetNotificationsAsync(setup.ClientWithUri));
        Assert.Equal(1, notification.Attempts);
        Assert.Equal("HTTP 500", notification.LastError);
        Assert.Equal(now + BackchannelLogoutOptions.InitialRetryDelay, notification.NextAttemptAt, TimeSpan.FromMilliseconds(1));

        // Not due yet.
        await factory.DeliverBackchannelLogoutsAsync(now + TimeSpan.FromSeconds(5));
        Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));

        // The delay doubles.
        var second = now + BackchannelLogoutOptions.InitialRetryDelay;
        await factory.DeliverBackchannelLogoutsAsync(second);
        notification = Assert.Single(await GetNotificationsAsync(setup.ClientWithUri));
        Assert.Equal(2, notification.Attempts);
        Assert.Equal(second + (2 * BackchannelLogoutOptions.InitialRetryDelay), notification.NextAttemptAt, TimeSpan.FromMilliseconds(1));

        // Delivered once the Client accepts it.
        factory.BackchannelLogoutHandler.Respond(setup.LogoutUri, HttpStatusCode.OK);
        await factory.DeliverBackchannelLogoutsAsync(notification.NextAttemptAt);

        Assert.Equal(3, factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri).Count);
        Assert.Empty(await GetNotificationsAsync(setup.ClientWithUri));
    }

    [Fact]
    public async Task FailedDelivery_IsGivenUpAfterMaxAttempts()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        factory.BackchannelLogoutHandler.Respond(setup.LogoutUri, HttpStatusCode.ServiceUnavailable);
        var maxAttempts = new BackchannelLogoutOptions().MaxAttempts;

        await EndSessionAsync(setup);
        var now = DateTime.UtcNow;

        // Every attempt after the longest delay, but all within the maximum age.
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await factory.DeliverBackchannelLogoutsAsync(now + (attempt * BackchannelLogoutOptions.MaxRetryDelay) + TimeSpan.FromMinutes(attempt));
        }

        Assert.Equal(maxAttempts, factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri).Count);
        Assert.Empty(await GetNotificationsAsync(setup.ClientWithUri));

        var failed = await GetAuditEventsAsync(setup.ClientWithUri, AuditEventTypes.Clients.BackchannelLogoutFailed);
        var auditEvent = Assert.Single(failed);
        Assert.Equal(AuditOutcome.Failed, auditEvent.Outcome);
        Assert.Contains($"User {setup.UserId}", auditEvent.Details, StringComparison.Ordinal);
        Assert.Contains("HTTP 503", auditEvent.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedDelivery_IsGivenUpAfterMaxAge()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        factory.BackchannelLogoutHandler.Respond(setup.LogoutUri, HttpStatusCode.BadGateway);

        await EndSessionAsync(setup);
        await factory.DeliverBackchannelLogoutsAsync(DateTime.UtcNow + new BackchannelLogoutOptions().MaxAge + TimeSpan.FromMinutes(1));

        Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));
        Assert.Empty(await GetNotificationsAsync(setup.ClientWithUri));
        Assert.Single(await GetAuditEventsAsync(setup.ClientWithUri, AuditEventTypes.Clients.BackchannelLogoutFailed));
    }

    [Fact]
    public async Task ClientWithoutUriAnyMore_IsNotNotified()
    {
        var setup = await SetUpSessionWithGrantsAsync();

        await EndSessionAsync(setup);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var client = await context.Clients.SingleAsync(client => client.Id == setup.ClientWithUri, TestContext.Current.CancellationToken);
            client.SetBackchannelLogoutUri(null, allowHttp: false);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await factory.DeliverBackchannelLogoutsAsync();

        Assert.Empty(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));
        Assert.Empty(await GetNotificationsAsync(setup.ClientWithUri));
    }

    [Fact]
    public async Task ConcurrentDeliveries_SendEachNotificationOnce()
    {
        var setups = new List<SessionSetup>();

        for (var i = 0; i < 6; i++)
        {
            var setup = await SetUpSessionWithGrantsAsync();
            await EndSessionAsync(setup);
            setups.Add(setup);
        }

        factory.BackchannelLogoutHandler.Delay = TimeSpan.FromMilliseconds(300);

        try
        {
            // Like several instances: each claims what the others do not hold.
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => factory.DeliverBackchannelLogoutsAsync()));
        }
        finally
        {
            factory.BackchannelLogoutHandler.Delay = TimeSpan.Zero;
        }

        foreach (var setup in setups)
        {
            Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));
            Assert.Empty(await GetNotificationsAsync(setup.ClientWithUri));
        }
    }

    [Fact]
    public async Task ClaimedNotification_IsTakenOverAfterClaimEnded()
    {
        var setup = await SetUpSessionWithGrantsAsync();
        await EndSessionAsync(setup);
        var now = DateTime.UtcNow;

        // An instance claimed it and stopped before saving the outcome.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            await context.BackchannelLogoutNotifications
                .Where(notification => notification.ClientId == setup.ClientWithUri)
                .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.NextAttemptAt, now + new BackchannelLogoutOptions().ClaimDuration), TestContext.Current.CancellationToken);
        }

        await factory.DeliverBackchannelLogoutsAsync(now);
        Assert.Empty(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));

        await factory.DeliverBackchannelLogoutsAsync(now + new BackchannelLogoutOptions().ClaimDuration);
        Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));
    }

    [Fact]
    public void Options_OutOfRange_AreRejected()
    {
        var validator = new BackchannelLogoutOptionsValidator();

        Assert.True(validator.Validate(null, new BackchannelLogoutOptions()).Succeeded);

        var result = validator.Validate(null, new BackchannelLogoutOptions
        {
            Timeout = TimeSpan.Zero,
            MaxAttempts = 0,
            MaxAge = TimeSpan.FromDays(8),
            PollInterval = TimeSpan.FromHours(1),
            BatchSize = 101
        });

        Assert.Equal(5, result.Failures!.Count());
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(10, 3600)]
    [InlineData(50, 3600)]
    public void RetryDelay_DoublesUpToOneHour(int attempts, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), BackchannelLogoutOptions.GetRetryDelay(attempts));
    }

    /// <summary>
    ///     Checks that the Session of the setup and all of its Grants are revoked, and that exactly the Client with a
    ///     Back-Channel Logout URI is notified, once, although it has two Grants in the Session.
    /// </summary>
    private async Task AssertEndedAndNotifiedAsync(SessionSetup setup)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            var session = await context.Sessions.AsNoTracking().SingleAsync(session => session.Id == setup.SessionId, TestContext.Current.CancellationToken);
            Assert.NotNull(session.RevokedAt);

            var grants = await context.Grants.AsNoTracking().Where(grant => grant.SessionId == setup.SessionId).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, grants.Count);
            Assert.All(grants, grant => Assert.NotNull(grant.RevokedAt));

            var notification = Assert.Single(await context.BackchannelLogoutNotifications.AsNoTracking()
                .Where(notification => notification.UserId == setup.UserId)
                .ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(setup.ClientWithUri, notification.ClientId);
            Assert.Equal(session.Sid, notification.Sid);
        }

        await factory.DeliverBackchannelLogoutsAsync();

        var received = Assert.Single(factory.BackchannelLogoutHandler.ReceivedFor(setup.LogoutUri));
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, received.LogoutToken!);
        Assert.Equal(setup.UserId.ToString(), claims.GetProperty("sub").GetString());
    }

    private async Task<SessionSetup> SetUpSessionWithGrantsAsync(string password = Password)
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var sessionId = (await GetSessionAsync(userId)).Id;
        var (clientWithUri, _, logoutUri) = await CreateClientAsync();
        var (clientWithoutUri, _) = await factory.CreatePublicClientAsync("openid");

        await CreateGrantAsync(userId, sessionId, clientWithUri);
        await CreateGrantAsync(userId, sessionId, clientWithUri);
        await CreateGrantAsync(userId, sessionId, clientWithoutUri);

        return new SessionSetup(userId, sessionId, sessionToken, clientWithUri, clientWithoutUri, logoutUri);
    }

    private async Task EndSessionAsync(SessionSetup setup)
    {
        using (await LogoutAsync(setup.SessionToken, idTokenHint: null, confirm: true))
        {
        }

        Assert.Single(await GetNotificationsAsync(setup.ClientWithUri));
    }

    private async Task<(Guid UserId, string SessionToken)> SignInAsync()
    {
        var userId = await factory.CreateUserAsync();

        return (userId, await factory.CreateSessionAsync(userId));
    }

    private async Task<(Guid ClientId, string RedirectUri, string LogoutUri)> CreateClientAsync()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var logoutUri = redirectUri.Replace("/callback", "/backchannel-logout", StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.SetBackchannelLogoutUri(logoutUri, allowHttp: false);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (clientId, redirectUri, logoutUri);
    }

    /// <summary>
    ///     Creates a Grant with an Access Token like exchanging a code issued in the Session does.
    /// </summary>
    private async Task CreateGrantAsync(Guid userId, Guid sessionId, Guid clientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var now = DateTime.UtcNow;
        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        var code = AuthorizationCode.Create(
            secretHasher.Hash(Guid.NewGuid().ToString()), client, user, sessionId, null, ["openid"], "test-challenge", null, now, [AuthenticationMethods.Password], now, TimeSpan.FromMinutes(1));
        var grant = Grant.Create(code, now, TimeSpan.FromDays(1));
        var accessToken = AccessToken.Create(grant.Id, secretHasher.Hash(Guid.NewGuid().ToString()), ["openid"], now, TimeSpan.FromHours(1));

        context.AddRange(code, grant, accessToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> IssueAdminTokenAsync()
    {
        var administratorId = await factory.CreateAdministratorAsync();

        return await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<GetClientResponse> GetClientAsync(Guid clientId, string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/clients/{clientId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GetClientResponse>(
            new JsonSerializerOptions(JsonOptions) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } },
            TestContext.Current.CancellationToken))!;
    }

    /// <summary>
    ///     Signs out at the end-session endpoint: right away with the ID Token of the Session as hint, otherwise after
    ///     confirming.
    /// </summary>
    private async Task<HttpResponseMessage> LogoutAsync(string sessionToken, string? idTokenHint, bool confirm = false)
    {
        var parameters = new Dictionary<string, string?>();

        if (idTokenHint is not null)
        {
            parameters["id_token_hint"] = idTokenHint;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("/oauth/logout", parameters));
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        if (!confirm)
        {
            return response;
        }

        using (response)
        {
            var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(response);

            using var confirmation = new HttpRequestMessage(HttpMethod.Post, "/oauth/logout")
            {
                Content = new FormUrlEncodedContent(
                [
                    new("decision", "sign-out"),
                    new("__RequestVerificationToken", antiforgeryToken)
                ])
            };
            confirmation.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

            return await _client.SendAsync(confirmation, TestContext.Current.CancellationToken);
        }
    }

    private async Task<Session> GetSessionAsync(Guid userId)
        => (await GetSessionsAsync(userId)).Single();

    private async Task<List<Session>> GetSessionsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Sessions.AsNoTracking().Where(session => session.UserId == userId).ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<BackchannelLogoutNotification>> GetNotificationsAsync(Guid clientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.BackchannelLogoutNotifications.AsNoTracking()
            .Where(notification => notification.ClientId == clientId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetAuditEventsAsync(Guid clientId, string type)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.TargetType == AuditTargetTypes.Client && auditEvent.TargetId == clientId.ToString() && auditEvent.Type == type)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> GetAuditTypesAsync(Guid userId, string type)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.TargetType == AuditTargetTypes.User && auditEvent.TargetId == userId.ToString() && auditEvent.Type == type)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     A User with a Session in which two Clients received tokens: one with a Back-Channel Logout URI and two
    ///     Grants, one without a URI.
    /// </summary>
    private sealed record SessionSetup(
        Guid UserId,
        Guid SessionId,
        string SessionToken,
        Guid ClientWithUri,
        Guid ClientWithoutUri,
        string LogoutUri);
}
