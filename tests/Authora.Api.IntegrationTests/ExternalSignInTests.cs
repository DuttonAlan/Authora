// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the sign-in through external OpenID Connect identity providers against a fake provider: the flow with
///     state, nonce and PKCE, the verification of the ID Token, the second factor, linking and unlinking at the account
///     pages, and the admin API.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ExternalSignInTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // Cookies are sent explicitly, so each request uses exactly the cookies of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task SignIn_LinkedUser_StartsSessionThroughTheProvider()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var identityId = await factory.LinkExternalIdentityAsync(userId, providerId, "subject-1");

        var (authorizationUrl, flowCookie, start) = await StartAsync(slug, SecurityModel.PagePath);

        Assert.Equal(provider.AuthorizationEndpoint, authorizationUrl.GetLeftPart(UriPartial.Path));
        var cookie = start.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{ExternalSignInCookie.Name}=", StringComparison.Ordinal))
            .ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", cookie, StringComparison.Ordinal);
        Assert.Contains("max-age=600", cookie, StringComparison.Ordinal);

        var query = QueryHelpers.ParseQuery(authorizationUrl.Query);
        Assert.Equal($"{AuthoraApiFactory.Issuer}/account/external/{slug}/callback", query["redirect_uri"].Single());
        Assert.Equal("openid email profile", query["scope"].Single());

        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-1"), flowCookie);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(SecurityModel.PagePath, callback.Headers.Location?.OriginalString);
        Assert.NotNull(ReadCookie(callback, SessionCookie.Name));
        Assert.True(IsDeleted(callback, ExternalSignInCookie.Name));

        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal("fed", session.AuthenticationMethod);

        // client_secret_basic with the PKCE verifier; the secret never travels in the body.
        var tokenRequest = Assert.Single(provider.TokenRequests);
        Assert.Equal("Basic", tokenRequest.Authorization?.Scheme);
        Assert.False(tokenRequest.Form.ContainsKey("client_secret"));
        Assert.True(tokenRequest.Form.ContainsKey("code_verifier"));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var identity = await context.ExternalIdentities.AsNoTracking().SingleAsync(identity => identity.Id == identityId, TestContext.Current.CancellationToken);
        Assert.NotNull(identity.LastUsedAt);

        var signIn = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.SignIn, userId.ToString()));
        Assert.Equal(AuditOutcome.Succeeded, signIn.Outcome);
        Assert.Contains("(fed)", signIn.Details, StringComparison.Ordinal);
        Assert.Contains($"identity provider '{slug}'", signIn.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ContinuesTheAuthorizationRequest_AndTheIdTokenNamesFed()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-authorize");
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        var returnUrl = QueryHelpers.AddQueryString("/oauth/authorize", parameters);

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, returnUrl);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-authorize"), flowCookie);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(returnUrl, callback.Headers.Location?.OriginalString);

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, ReadCookie(callback, SessionCookie.Name)!);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        Assert.Equal(["fed"], claims.GetProperty("amr").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("urn:authora:acr:pwd", claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task SignIn_WithSecondFactor_StillRequiresIt()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        factory.SecondFactor.Enroll(userId, "246810");
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-mfa");

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-mfa"), flowCookie);

        // A page instead of a redirect, so the browser sends the SameSite=Strict cookie of the pending sign-in.
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Null(ReadCookie(callback, SessionCookie.Name));
        var pendingToken = ReadCookie(callback, PendingSignInCookie.Name);
        Assert.NotNull(pendingToken);
        var html = await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("http-equiv=\"refresh\"", html, StringComparison.Ordinal);
        Assert.Contains(SecondFactorModel.PagePath, html, StringComparison.Ordinal);
        Assert.Empty(await GetSessionsAsync(userId));

        using var completed = await SubmitSecondFactorAsync(pendingToken, "246810");

        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.NotNull(ReadCookie(completed, SessionCookie.Name));
        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal($"fed {FakeSecondFactor.MethodName}", session.AuthenticationMethod);
    }

    [Fact]
    public async Task SignIn_NotLinkedAccount_WithoutSignUp_IsRefused()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "unknown-subject"), flowCookie);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Null(ReadCookie(callback, SessionCookie.Name));
        Assert.Contains(ExternalCallbackModel.GetNotPossibleMessage("Test IdP"), await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        var failure = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()));
        Assert.Equal(AuditOutcome.Failed, failure.Outcome);
        Assert.DoesNotContain("unknown-subject", failure.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_WithWrongState_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-state");

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        var response = provider.Authorize(authorizationUrl, "subject-state");
        response["state"] = "forged-state";

        using var callback = await CallbackAsync(slug, response, flowCookie);

        await AssertFailedAsync(callback, userId);
        Assert.Empty(provider.TokenRequests);
        Assert.Contains(
            await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()),
            auditEvent => auditEvent.Details!.Contains("state", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Callback_WithoutTheFlowCookie_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-no-cookie");

        var (authorizationUrl, _, _) = await StartAsync(slug, SecurityModel.PagePath);

        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-no-cookie"), flowCookie: null);

        await AssertFailedAsync(callback, userId);
        Assert.Empty(provider.TokenRequests);
    }

    [Fact]
    public async Task Callback_WithAnErrorOfTheProvider_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        var state = QueryHelpers.ParseQuery(authorizationUrl.Query)["state"].Single()!;

        using var callback = await CallbackAsync(
            slug,
            new Dictionary<string, string> { ["error"] = "access_denied", ["state"] = state },
            flowCookie);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Contains(ExternalCallbackModel.FailedMessage, await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(provider.TokenRequests);
        Assert.Contains(
            await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()),
            auditEvent => auditEvent.Details!.Contains("access_denied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Callback_WithTheIssOfAnotherProvider_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-mixup");

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        var response = provider.Authorize(authorizationUrl, "subject-mixup");
        response["iss"] = "https://attacker.example";

        using var callback = await CallbackAsync(slug, response, flowCookie);

        await AssertFailedAsync(callback, userId);
        Assert.Empty(provider.TokenRequests);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("iss")]
    [InlineData("aud")]
    [InlineData("azp")]
    [InlineData("expired")]
    [InlineData("iat")]
    [InlineData("signature")]
    [InlineData("unknown-key")]
    public async Task Callback_WithAnInvalidIdToken_IsRejected(string defect)
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-token");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        switch (defect)
        {
            case "nonce":
                provider.ChangeClaims = claims => claims["nonce"] = "another-nonce";
                break;
            case "iss":
                provider.ChangeClaims = claims => claims["iss"] = "https://attacker.example";
                break;
            case "aud":
                provider.ChangeClaims = claims => claims["aud"] = "another-client";
                break;
            case "azp":
                provider.ChangeClaims = claims => claims["azp"] = "another-client";
                break;
            case "expired":
                provider.ChangeClaims = claims =>
                {
                    claims["iat"] = now - 3600;
                    claims["exp"] = now - 1800;
                };
                break;
            case "iat":
                // Valid for a long time, but issued long before the sign-in started.
                provider.ChangeClaims = claims => claims["iat"] = now - 7200;
                break;
            case "signature":
                // Signed with another key under the Id of the published one.
                provider.OtherSigningKey = FakeOidcProvider.CreateKey(provider.SigningKey.KeyId);
                break;
            default:
                provider.OtherSigningKey = FakeOidcProvider.CreateKey("not-published");
                break;
        }

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);

        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-token"), flowCookie);

        await AssertFailedAsync(callback, userId);
        Assert.Single(provider.TokenRequests);

        var failure = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()));
        Assert.Contains("ID Token", failure.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_WithClientSecretPostOnly_SendsTheSecretInTheBody()
    {
        var provider = factory.ExternalProviders.Create();
        provider.TokenEndpointAuthMethods = ["client_secret_post"];
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-post");

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-post"), flowCookie);

        Assert.NotNull(ReadCookie(callback, SessionCookie.Name));
        var tokenRequest = Assert.Single(provider.TokenRequests);
        Assert.Null(tokenRequest.Authorization);
        Assert.Equal(provider.ClientSecret, tokenRequest.Form["client_secret"]);
    }

    [Fact]
    public async Task DisabledProvider_IsHidden_AndItsRunningFlowsAreRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider, "Disabled IdP");
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-disabled");
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);

        using (var disable = await SendAdminAsync(HttpMethod.Post, $"/api/identity-providers/{providerId}/disable", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        }

        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-disabled"), flowCookie);

        await AssertFailedAsync(callback, userId);

        using var start = await GetAsync(ExternalPath(slug), sessionToken: null);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);

        using var login = await GetAsync(SessionAuthenticationHandler.SignInPath, sessionToken: null);
        Assert.DoesNotContain("Disabled IdP", await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginPage_ShowsALinkPerEnabledProvider()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider, "Shown IdP");

        using var login = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2Faccount%2Fsecurity", sessionToken: null);
        var html = await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Sign in with Shown IdP", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/account/external/{slug}?returnUrl=%2Faccount%2Fsecurity\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Link_WhileSignedIn_LinksTheAccount_WhichThenSignsIn()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        var (authorizationUrl, flowCookie) = await StartLinkAsync(slug, sessionToken);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-link", "user@idp.example"), flowCookie, sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal($"{ExternalAccountsModel.PagePath}?status=linked", callback.Headers.Location?.OriginalString);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var identity = await context.ExternalIdentities.AsNoTracking().SingleAsync(identity => identity.UserId == userId, TestContext.Current.CancellationToken);

            Assert.Equal(providerId, identity.ProviderId);
            Assert.Equal("subject-link", identity.Subject);
            Assert.Equal("user@idp.example", identity.Email);
        }

        var linked = Assert.Single(await GetEventsAsync(AuditEventTypes.ExternalIdentities.Linked, userId.ToString()));
        Assert.Equal(userId, linked.ActorUserId);

        var page = await GetPageAsync($"{ExternalAccountsModel.PagePath}?status=linked", sessionToken);
        Assert.Contains(ExternalAccountsModel.LinkedMessage, page, StringComparison.Ordinal);
        Assert.Contains("user@idp.example", page, StringComparison.Ordinal);

        // The linked account now signs in.
        var (signInUrl, signInCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        using var signIn = await CallbackAsync(slug, provider.Authorize(signInUrl, "subject-link"), signInCookie);

        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
    }

    [Fact]
    public async Task Link_WithoutRecentAuthentication_AsksToSignInAgain()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-20));

        using var response = await GetAsync(ExternalLinkModel.GetPath(slug), sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(SessionAuthenticationHandler.SignInPath, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Null(ReadCookie(response, ExternalSignInCookie.Name));
    }

    [Fact]
    public async Task Link_InAnotherSession_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (otherUserId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var otherSessionToken = await factory.CreateSessionAsync(otherUserId);

        var (authorizationUrl, flowCookie) = await StartLinkAsync(slug, sessionToken);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-other-session"), flowCookie, otherSessionToken);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Contains(ExternalCallbackModel.FailedMessage, await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetIdentitiesAsync(userId));
        Assert.Empty(await GetIdentitiesAsync(otherUserId));
    }

    [Fact]
    public async Task Link_OfAnAccountLinkedToAnotherUser_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (ownerId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(ownerId, providerId, "subject-taken");
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        var (authorizationUrl, flowCookie) = await StartLinkAsync(slug, sessionToken);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-taken"), flowCookie, sessionToken);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Contains(ExternalCallbackModel.LinkConflictMessage, await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetIdentitiesAsync(userId));
        Assert.Single(await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, userId.ToString()));
    }

    [Fact]
    public async Task Unlink_RemovesTheLink()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var identityId = await factory.LinkExternalIdentityAsync(userId, providerId, "subject-unlink");
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var page = await GetAsync(ExternalAccountsModel.PagePath, sessionToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ExternalAccountsModel.PagePath}?handler=Unlink")
        {
            Content = new FormUrlEncodedContent(
            [
                new("ExternalIdentityId", identityId.ToString()),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{ExternalAccountsModel.PagePath}?status=unlinked", response.Headers.Location?.OriginalString);
        Assert.Empty(await GetIdentitiesAsync(userId));
        Assert.Single(await GetEventsAsync(AuditEventTypes.ExternalIdentities.Unlinked, userId.ToString()));

        // The account no longer signs in.
        var (authorizationUrl, flowCookie, _) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-unlink"), flowCookie);

        Assert.Null(ReadCookie(callback, SessionCookie.Name));
    }

    [Fact]
    public async Task AdminApi_ManagesProviders_WithoutEverReturningTheSecret()
    {
        var provider = factory.ExternalProviders.Create();
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var slug = $"idp-{Guid.NewGuid():N}"[..20];
        const string secret = "very-secret-value-of-the-provider";

        using var create = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Company login",
            slug,
            preset = "Generic",
            issuer = provider.Issuer,
            clientId = provider.ClientId,
            clientSecret = secret,
            sortOrder = 5
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal($"{AuthoraApiFactory.Issuer}/account/external/{slug}/callback", created.GetProperty("redirectUri").GetString());

        using var get = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{id}", token);
        var body = await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.True(json.GetProperty("hasClientSecret").GetBoolean());
        Assert.True(json.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(["openid", "email", "profile"], json.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()));

        using var list = await SendAdminAsync(HttpMethod.Get, "/api/identity-providers", token);
        Assert.DoesNotContain(secret, await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        using var update = await SendAdminAsync(HttpMethod.Put, $"/api/identity-providers/{id}", token, new { name = "Renamed login", scopes = new[] { "email" } });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using var setSecret = await SendAdminAsync(HttpMethod.Put, $"/api/identity-providers/{id}/secret", token, new { clientSecret = "another-secret-value" });
        Assert.Equal(HttpStatusCode.NoContent, setSecret.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var stored = await context.IdentityProviders.AsNoTracking().SingleAsync(stored => stored.Id == id, TestContext.Current.CancellationToken);
            var protector = scope.ServiceProvider.GetRequiredService<IProviderSecretProtector>();

            Assert.Equal("Renamed login", stored.Name);
            Assert.Equal("openid email", stored.Scopes);
            Assert.DoesNotContain("another-secret-value", stored.ProtectedClientSecret, StringComparison.Ordinal);
            Assert.Equal("another-secret-value", protector.Unprotect(stored.ProtectedClientSecret!));

            var events = await context.AuditEvents.AsNoTracking()
                .Where(auditEvent => auditEvent.TargetId == id.ToString())
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Contains(events, auditEvent => auditEvent.Type == AuditEventTypes.IdentityProviders.Created);
            Assert.Equal(2, events.Count(auditEvent => auditEvent.Type == AuditEventTypes.IdentityProviders.Updated));
            Assert.All(events, auditEvent => Assert.DoesNotContain("secret-value", auditEvent.Details ?? string.Empty, StringComparison.Ordinal));
        }

        using var removeSecret = await SendAdminAsync(HttpMethod.Delete, $"/api/identity-providers/{id}/secret", token);
        Assert.Equal(HttpStatusCode.NoContent, removeSecret.StatusCode);

        using var afterRemoval = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{id}", token);
        Assert.False((await afterRemoval.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("hasClientSecret").GetBoolean());

        using var delete = await SendAdminAsync(HttpMethod.Delete, $"/api/identity-providers/{id}", token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var gone = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{id}", token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task AdminApi_Create_WithAnUnusableIssuer_Returns400()
    {
        var provider = factory.ExternalProviders.Create();
        provider.DiscoveryIssuer = "https://another-issuer.example";
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var mismatch = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Mismatch",
            slug = $"idp-{Guid.NewGuid():N}"[..20],
            preset = "Generic",
            issuer = provider.Issuer,
            clientId = "client"
        });

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);

        using var unreachable = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Unreachable",
            slug = $"idp-{Guid.NewGuid():N}"[..20],
            preset = "Generic",
            issuer = "https://unreachable.idp.example",
            clientId = "client"
        });

        Assert.Equal(HttpStatusCode.BadRequest, unreachable.StatusCode);

        using var plainHttp = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Plain HTTP",
            slug = $"idp-{Guid.NewGuid():N}"[..20],
            preset = "Generic",
            issuer = "http://insecure.idp.example",
            clientId = "client"
        });

        Assert.Equal(HttpStatusCode.BadRequest, plainHttp.StatusCode);
    }

    [Fact]
    public async Task AdminApi_Create_WithAUsedSlug_Returns409()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var response = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Duplicate",
            slug,
            preset = "Generic",
            issuer = provider.Issuer,
            clientId = provider.ClientId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AdminApi_Delete_WithLinkedAccounts_Returns409()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, _) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-delete");
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var response = await SendAdminAsync(HttpMethod.Delete, $"/api/identity-providers/{providerId}", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var get = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{providerId}", token);
        Assert.Equal(1, (await get.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("linkedIdentities").GetInt32());
    }

    [Fact]
    public async Task AdminApi_RequiresTheManagePermission_ToChangeProviders()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, _) = await factory.CreateIdentityProviderAsync(provider);
        var readerId = await factory.CreateUserAsync(PermissionNames.IdentityProviders.Read);
        var token = await factory.IssueAccessTokenAsync(readerId, [AdminApiScope.Name]);

        using var get = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{providerId}", token);
        using var disable = await SendAdminAsync(HttpMethod.Post, $"/api/identity-providers/{providerId}/disable", token);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, disable.StatusCode);
    }

    [Fact]
    public async Task DeletingAUser_RemovesTheirLinks()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, _) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.LinkExternalIdentityAsync(userId, providerId, "subject-deleted-user");
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var response = await SendAdminAsync(HttpMethod.Delete, $"/api/users/{userId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetIdentitiesAsync(userId));
    }

    [Fact]
    public void GooglePreset_FixesTheIssuer()
    {
        var provider = IdentityProvider.Create("Google", "google", IdentityProviderPreset.Google, "https://ignored.example", "client");

        Assert.Equal(IdentityProvider.GoogleIssuer, provider.Issuer);
        Assert.Throws<Core.Domain.Exceptions.DomainValidationException>(() => provider.SetIssuer("https://login.example.com"));
    }

    [Fact]
    public async Task HttpAuthorizationEndpoint_IsRejected_WhenSavedAndBeforeTheRedirect()
    {
        var provider = factory.ExternalProviders.Create();
        provider.HttpAuthorizationEndpoint = true;
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var create = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "HTTP sign-in page",
            slug = $"idp-{Guid.NewGuid():N}"[..20],
            preset = "Generic",
            issuer = provider.Issuer,
            clientId = provider.ClientId
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        // A provider whose document changed after it was saved.
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);

        using var start = await GetAsync(ExternalPath(slug), sessionToken: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, start.StatusCode);
        Assert.Null(start.Headers.Location);
        Assert.Null(ReadCookie(start, ExternalSignInCookie.Name));
    }

    [Fact]
    public async Task OversizedKeySet_IsRejected()
    {
        var provider = factory.ExternalProviders.Create();
        provider.KeySetPadding = 600 * 1024;
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var create = await SendAdminAsync(HttpMethod.Post, "/api/identity-providers", token, new
        {
            name = "Huge key set",
            slug = $"idp-{Guid.NewGuid():N}"[..20],
            preset = "Generic",
            issuer = provider.Issuer,
            clientId = provider.ClientId
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Callback_IsRateLimited_AlsoForGet()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:RateLimiting:SignInPermitsPerMinute", "1"));
        using var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        using var first = await client.GetAsync($"{ExternalPath("unknown")}/callback?state=x&code=y", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync($"{ExternalPath("unknown")}/callback?state=x&code=y", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        // The sign-in page itself is still only limited for POST.
        using var login = await client.GetAsync(SessionAuthenticationHandler.SignInPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static string ExternalPath(string slug) => $"/account/external/{slug}";

    private async Task<(Uri AuthorizationUrl, string FlowCookie, HttpResponseMessage Response)> StartAsync(string slug, string returnUrl)
    {
        var response = await GetAsync($"{ExternalPath(slug)}?returnUrl={Uri.EscapeDataString(returnUrl)}", sessionToken: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return (response.Headers.Location!, $"{ExternalSignInCookie.Name}={ReadCookie(response, ExternalSignInCookie.Name)}", response);
    }

    private async Task<(Uri AuthorizationUrl, string FlowCookie)> StartLinkAsync(string slug, string sessionToken)
    {
        using var response = await GetAsync(ExternalLinkModel.GetPath(slug), sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return (response.Headers.Location!, $"{ExternalSignInCookie.Name}={ReadCookie(response, ExternalSignInCookie.Name)}");
    }

    private async Task<HttpResponseMessage> CallbackAsync(
        string slug,
        Dictionary<string, string> parameters,
        string? flowCookie,
        string? sessionToken = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            QueryHelpers.AddQueryString($"{ExternalPath(slug)}/callback", parameters!));

        var cookies = new List<string>();

        if (flowCookie is not null)
        {
            cookies.Add(flowCookie);
        }

        if (sessionToken is not null)
        {
            cookies.Add($"{SessionCookie.Name}={sessionToken}");
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AssertFailedAsync(HttpResponseMessage callback, Guid userId)
    {
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Null(ReadCookie(callback, SessionCookie.Name));
        Assert.Null(ReadCookie(callback, PendingSignInCookie.Name));
        Assert.True(IsDeleted(callback, ExternalSignInCookie.Name));
        Assert.Contains(ExternalCallbackModel.FailedMessage, await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetSessionsAsync(userId));
    }

    private async Task<HttpResponseMessage> SubmitSecondFactorAsync(string pendingToken, string code)
    {
        using var page = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2F", sessionToken: null);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, SecondFactorModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("Method", FakeSecondFactor.MethodName),
                new("Code", code),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{PendingSignInCookie.Name}={pendingToken}; {antiforgeryCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAdminAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
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

    private async Task<List<ExternalIdentity>> GetIdentitiesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.ExternalIdentities.AsNoTracking()
            .Where(identity => identity.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetEventsAsync(string type, string targetId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == targetId)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
