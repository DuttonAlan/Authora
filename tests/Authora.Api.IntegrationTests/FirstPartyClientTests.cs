// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients.Get;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that first-party Clients get the Authorization Code without the consent screen, and that only Users with
///     the dedicated Permission can mark a Client as first-party (TD-012).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class FirstPartyClientTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task FirstPartyClient_GetsCodeWithoutConsent()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync();

        using var response = await AuthorizationCodeFlow.GetAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        var code = AssertRedirectedCode(response, redirectUri);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        Assert.True(tokens.TryGetProperty("id_token", out _));
    }

    [Fact]
    public async Task FirstPartyClient_WithPromptNone_GetsCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync();
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedCode(response, redirectUri);
    }

    [Fact]
    public async Task FirstPartyClient_WithPromptConsent_ShowsConsent()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync();
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "consent";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FirstPartyClient_WithoutSession_RedirectsToSignIn()
    {
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync();

        using var response = await AuthorizationCodeFlow.GetAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/account/login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThirdPartyClient_StillShowsConsent()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        using var response = await AuthorizationCodeFlow.GetAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MarkFirstParty_IsRecordedAndCanBeUndone()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/first-party", adminToken));
        Assert.True((await GetClientAsync(clientId, adminToken)).IsFirstParty);

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}/first-party", adminToken));
        Assert.False((await GetClientAsync(clientId, adminToken)).IsFirstParty);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var types = await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([AuditEventTypes.Clients.MarkedFirstParty, AuditEventTypes.Clients.MarkedThirdParty], types);
    }

    [Fact]
    public async Task MarkFirstParty_WithOnlyUpdatePermission_IsForbidden()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Clients.Read, PermissionNames.Clients.Update);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/first-party", token));
        Assert.False((await GetClientAsync(clientId, token)).IsFirstParty);
    }

    [Fact]
    public async Task FirstPartyClient_WithOnlyUpdatePermission_GetsNoRedirectUriOrScope()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Clients.Read, PermissionNames.Clients.Update);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        Guid adminUiClientId;
        Guid scopeId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            adminUiClientId = await context.Clients
                .Where(client => client.RedirectUris.Any(uri => uri.Value == $"{AuthoraApiFactory.Issuer}/admin/callback"))
                .Select(client => client.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
            scopeId = await context.Scopes
                .Where(existing => existing.Name == "openid")
                .Select(existing => existing.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }

        // Otherwise the codes of the admin interface, which are issued without a click, could be sent anywhere.
        Assert.Equal(
            HttpStatusCode.Conflict,
            await SendAsync(HttpMethod.Post, $"/api/clients/{adminUiClientId}/redirect-uris", token, new { uri = "https://evil.example.com/callback" }));
        Assert.Equal(HttpStatusCode.Conflict, await SendAsync(HttpMethod.Put, $"/api/clients/{adminUiClientId}/scopes/{scopeId}", token));

        var client = await GetClientAsync(adminUiClientId, token);
        Assert.DoesNotContain("https://evil.example.com/callback", client.RedirectUris);

        // Third-party Clients can still be changed with the Permission.
        var (thirdPartyId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(
            HttpStatusCode.NoContent,
            await SendAsync(HttpMethod.Post, $"/api/clients/{thirdPartyId}/redirect-uris", token, new { uri = "https://app.example.com/other" }));
    }

    [Fact]
    public async Task MarkFirstParty_UnknownClient_IsNotFound()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(HttpMethod.Put, $"/api/clients/{Guid.CreateVersion7()}/first-party", adminToken));
    }

    private static string AssertRedirectedCode(HttpResponseMessage response, string redirectUri)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.StartsWith(redirectUri, location.ToString(), StringComparison.Ordinal);

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("test-state", query["state"].Single());

        return query["code"].Single()!;
    }

    private async Task<(Guid ClientId, string RedirectUri)> CreateFirstPartyClientAsync()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.SetFirstParty(true);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (clientId, redirectUri);
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private async Task<GetClientResponse> GetClientAsync(Guid clientId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/clients/{clientId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GetClientResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
    }
}
