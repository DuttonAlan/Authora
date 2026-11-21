// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that approving a third-party Client is remembered, extended by further Scopes, forced by
///     <c>prompt=consent</c>, and revoked by the User or an administrator together with the Client's tokens.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ConsentTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Approval_IsRemembered()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", "profile");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid profile");

        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        // Asked once; afterwards the code comes without the consent screen, also for fewer Scopes.
        var code = await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, sessionToken);
        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "profile"), sessionToken);

        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        Assert.Equal("openid profile", tokens.GetProperty("scope").GetString());

        var granted = await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Granted);
        var single = Assert.Single(granted);
        Assert.Equal(clientId, single.ClientId);
        Assert.Contains("added Scopes 'openid profile'", single.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptNone_WithRememberedConsent_GetsCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none";

        Assert.Equal("consent_required", await GetErrorAsync(parameters, sessionToken));

        parameters.Remove("prompt");
        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        parameters["prompt"] = "none";
        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, sessionToken);
    }

    [Fact]
    public async Task PromptConsent_ShowsConsentScreenDespiteRememberedConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        parameters["prompt"] = "consent";

        using (var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Approving the same Scopes again changes nothing, so it is not recorded again.
        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        Assert.Single(await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Granted));
    }

    [Fact]
    public async Task NewScope_ShowsConsentScreen_AndExtendsConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", "profile", "email");

        await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        var extended = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid email");

        using (var response = await AuthorizationCodeFlow.GetAsync(_client, extended, sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await AuthorizationCodeFlow.GetCodeAsync(_client, extended, sessionToken);
        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, extended, sessionToken);

        // profile was never approved.
        using (var response = await AuthorizationCodeFlow.GetAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid profile"), sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var granted = await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Granted);
        Assert.Equal(2, granted.Count);
        Assert.Contains("added Scopes 'email'", granted[1].Details, StringComparison.Ordinal);

        var consents = await GetConsentsAsync(userId, await AdminTokenAsync());
        var consent = Assert.Single(consents.EnumerateArray());
        Assert.Equal(
            ["email", "openid"],
            consent.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetProperty("name").GetString()));
        Assert.NotEqual(JsonValueKind.Null, consent.GetProperty("modifiedAt").ValueKind);
    }

    [Fact]
    public async Task FirstPartyClient_WithPromptConsent_StoresNoConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        await SetFirstPartyAsync(clientId);
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "consent";

        // Approving still works, but nothing is stored that the User could "remove" without effect.
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        Assert.Equal(0, (await GetConsentsAsync(userId, await AdminTokenAsync())).GetArrayLength());
        Assert.Empty(await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Granted));
    }

    [Fact]
    public async Task CodeIssuedBeforeRevocation_StaysInvalidAfterNewApproval()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        var pendingCode = await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/consents/{clientId}", await AdminTokenAsync()));

        // The User approves again before the old code expires: the new Consent must not revive the old code.
        var newCode = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        Assert.Equal("invalid_grant", await ExchangeErrorAsync(clientId, redirectUri, pendingCode));
        await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, newCode);
    }

    [Fact]
    public async Task FirstPartyClient_StoresNoConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        await SetFirstPartyAsync(clientId);

        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        Assert.Equal(0, (await GetConsentsAsync(userId, await AdminTokenAsync())).GetArrayLength());
    }

    [Fact]
    public async Task RevokeOnAccountPage_RevokesTokens_AndAsksAgain()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        var tokens = await AuthorizationCodeFlow.ExchangeAsync(
            _client, clientId, redirectUri, await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken));

        // Issued before the revocation, exchanged after it.
        var pendingCode = await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, sessionToken);

        string html;

        using (var page = await GetAccountPageAsync(sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains(clientId.ToString(), html, StringComparison.Ordinal);

            using var revoked = await PostAccountPageAsync(page, sessionToken, clientId);
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
            html = await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        Assert.Contains("no longer has access", html, StringComparison.Ordinal);
        Assert.DoesNotContain(clientId.ToString(), html, StringComparison.Ordinal);

        await AssertTokensRevokedAsync(clientId, tokens);
        Assert.Equal("invalid_grant", await ExchangeErrorAsync(clientId, redirectUri, pendingCode));

        using (var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var revokedEvent = Assert.Single(await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Revoked));
        Assert.Equal(userId, revokedEvent.UserId);
        // The account page acts through no Client; the revoked one is named in the Details.
        Assert.Null(revokedEvent.ClientId);
        Assert.Contains($"Client {clientId}, 1 Grant(s) revoked", revokedEvent.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountPage_RevokesOnlyOwnConsent()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        var ownerId = await factory.CreateUserAsync();
        var ownerSession = await factory.CreateSessionAsync(ownerId);
        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, ownerSession);

        // The other User approved another Client, so the page has a form to post.
        var otherSession = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (otherClientId, otherRedirectUri) = await factory.CreatePublicClientAsync("openid");
        await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(otherClientId, otherRedirectUri, "openid"), otherSession);

        using (var page = await GetAccountPageAsync(otherSession))
        {
            var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain(clientId.ToString(), html, StringComparison.Ordinal);

            using var revoked = await PostAccountPageAsync(page, otherSession, clientId);
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

            html = await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("no longer has access", html, StringComparison.Ordinal);
            Assert.Contains(otherClientId.ToString(), html, StringComparison.Ordinal);
        }

        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, ownerSession);
    }

    [Fact]
    public async Task AccountPage_WithoutConsents_SaysSo()
    {
        using var page = await GetAccountPageAsync(await factory.CreateSessionAsync(await factory.CreateUserAsync()));
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("You have not allowed any application", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountPage_WithoutSession_RedirectsToSignIn()
    {
        using var response = await _client.GetAsync("/account/consents", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/account/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_ListsAndRevokesConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        var tokens = await AuthorizationCodeFlow.ExchangeAsync(
            _client, clientId, redirectUri, await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken));

        var adminId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(adminId, [AdminApiScope.Name]);

        var consent = Assert.Single((await GetConsentsAsync(userId, adminToken)).EnumerateArray());
        Assert.Equal(clientId, consent.GetProperty("clientId").GetGuid());
        Assert.Equal("Test Client", consent.GetProperty("clientName").GetString());

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/consents/{clientId}", adminToken));

        Assert.Equal(0, (await GetConsentsAsync(userId, adminToken)).GetArrayLength());
        await AssertTokensRevokedAsync(clientId, tokens);

        // Idempotent.
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/consents/{clientId}", adminToken));

        var revokedEvent = Assert.Single(await GetAuditEventsAsync(userId, AuditEventTypes.Consents.Revoked));
        Assert.Equal(adminId, revokedEvent.UserId);
        Assert.NotEqual(clientId, revokedEvent.ClientId);
        Assert.Contains($"Client {clientId}", revokedEvent.Details, StringComparison.Ordinal);

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_UnknownUser_IsNotFound()
    {
        var adminToken = await AdminTokenAsync();
        var unknown = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(HttpMethod.Get, $"/api/users/{unknown}/consents", adminToken));
        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(HttpMethod.Delete, $"/api/users/{unknown}/consents/{Guid.CreateVersion7()}", adminToken));
    }

    [Fact]
    public async Task RevokeConsent_WithOnlyReadPermission_IsForbidden()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        var reader = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(PermissionNames.Users.Read), [AdminApiScope.Name]);

        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, $"/api/users/{userId}/consents", reader));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/consents/{clientId}", reader));

        await AuthorizationCodeFlow.GetCodeWithoutConsentAsync(_client, parameters, sessionToken);
    }

    [Fact]
    public async Task DeletedScope_IsAskedForAgain()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var adminToken = await AdminTokenAsync();
        var scopeName = $"orders.{Guid.NewGuid():N}";
        var scopeId = await CreateScopeAsync(scopeName, adminToken);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", scopeName);
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, $"openid {scopeName}");

        await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/scopes/{scopeId}?force=true", adminToken));
        await CreateScopeAsync(scopeName, adminToken);
        await AllowScopeAsync(clientId, scopeName);

        // A new Scope with the old Name is a different Scope, which the User has not approved.
        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_DeletesConsents()
    {
        var userId = await factory.CreateUserAsync();
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), await factory.CreateSessionAsync(userId));

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/users/{userId}", await AdminTokenAsync()));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        Assert.False(await context.Consents.AnyAsync(consent => consent.UserId == userId, TestContext.Current.CancellationToken));
    }

    private async Task<string> AdminTokenAsync()
    {
        return await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
    }

    private async Task<string?> GetErrorAsync(Dictionary<string, string?> parameters, string sessionToken)
    {
        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].Single();
    }

    private async Task<HttpResponseMessage> GetAccountPageAsync(string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/consents");
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostAccountPageAsync(HttpResponseMessage page, string sessionToken, Guid clientId)
    {
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/consents")
        {
            Content = new FormUrlEncodedContent(
            [
                new("clientId", clientId.ToString()),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AssertTokensRevokedAsync(Guid clientId, JsonElement tokens)
    {
        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", tokens.GetProperty("refresh_token").GetString())
        ]);

        using var refresh = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
        var error = await refresh.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Get, "/oauth/userinfo", tokens.GetProperty("access_token").GetString()!));
    }

    private async Task<string?> ExchangeErrorAsync(Guid clientId, string redirectUri, string code)
    {
        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", AuthorizationCodeFlow.CodeVerifier)
        ]);

        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        return error.GetProperty("error").GetString();
    }

    private async Task<JsonElement> GetConsentsAsync(Guid userId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}/consents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CreateScopeAsync(string name, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/scopes")
        {
            Content = JsonContent.Create(new { name, displayName = "Read your orders" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
    }

    private async Task AllowScopeAsync(Guid clientId, string scopeName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.Include(client => client.AllowedScopes).SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.AllowScope(await context.Scopes.SingleAsync(existing => existing.Name == scopeName, TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetFirstPartyAsync(Guid clientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.SetFirstParty(true);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private async Task<List<(Guid? UserId, Guid? ClientId, string? Details)>> GetAuditEventsAsync(Guid userId, string type)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var events = await context.AuditEvents
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == userId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => new { auditEvent.ActorUserId, auditEvent.ActorClientId, auditEvent.Details })
            .ToListAsync(TestContext.Current.CancellationToken);

        return events.Select(auditEvent => (auditEvent.ActorUserId, auditEvent.ActorClientId, auditEvent.Details)).ToList();
    }
}
