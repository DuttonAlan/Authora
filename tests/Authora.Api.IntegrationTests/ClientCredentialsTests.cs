// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the Client Credentials Grant: Confidential Clients that are allowed it obtain Access Tokens for
///     themselves, without a User, a Refresh Token or an ID Token, and only with Scopes they are allowed.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ClientCredentialsTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Secret = "client-credentials-test-secret-0123456789";

    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task AllowedClient_GetsAccessTokenOnly()
    {
        var (read, write) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read, write);

        using var response = await RequestTokenAsync(clientId, $"{read} {write}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("Bearer", tokens.GetProperty("token_type").GetString());
        Assert.Equal(3600, tokens.GetProperty("expires_in").GetInt32());
        Assert.Equal(string.Join(' ', new[] { read, write }.Order(StringComparer.Ordinal)), tokens.GetProperty("scope").GetString());
        Assert.False(tokens.TryGetProperty("refresh_token", out _));
        Assert.False(tokens.TryGetProperty("id_token", out _));

        Assert.Equal([AuditEventTypes.Clients.TokenIssued], await GetAuditTypesAsync(clientId));
    }

    [Fact]
    public async Task ClientAlsoAllowedRefreshTokens_GetsNoRefreshToken()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(
            GrantTypes.AuthorizationCode | GrantTypes.RefreshToken | GrantTypes.ClientCredentials,
            read);

        using var response = await RequestTokenAsync(clientId, read);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(tokens.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task NarrowerScope_IsGranted()
    {
        var (read, write) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read, write);

        using var response = await RequestTokenAsync(clientId, read);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(read, tokens.GetProperty("scope").GetString());

        var introspection = await IntrospectAsync(clientId, tokens.GetProperty("access_token").GetString()!);
        Assert.Equal(read, introspection.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task WithoutScope_GetsAllAllowedScopesExceptSystemScopes()
    {
        var (read, write) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read, write, "openid");

        using var response = await RequestTokenAsync(clientId, scope: null);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Join(' ', new[] { read, write }.Order(StringComparer.Ordinal)), tokens.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task WithoutScope_AllowedScopesTooLongTogether_AreRejected()
    {
        // 40 Names of 100 characters exceed the 4000 characters a Grant can store.
        var names = await CreateLongScopesAsync(40);
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, [.. names]);

        using var response = await RequestTokenAsync(clientId, scope: null);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_scope");
        Assert.Empty(await GetAuditTypesAsync(clientId));
    }

    [Fact]
    public async Task ManyScopes_AuditDetailsKeepTheExpiryAndAreVisiblyShortened()
    {
        var names = await CreateLongScopesAsync(15);
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, [.. names]);

        using var response = await RequestTokenAsync(clientId, scope: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var details = await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString() && auditEvent.Type == AuditEventTypes.Clients.TokenIssued)
            .Select(auditEvent => auditEvent.Details)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(details);
        Assert.Equal(AuditEvent.DetailsMaxLength, details.Length);
        Assert.Contains("expires at ", details, StringComparison.Ordinal);
        Assert.Contains("15 Scopes '", details, StringComparison.Ordinal);
        Assert.EndsWith("…' (shortened)", details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScopeNotAllowed_IsRejected()
    {
        var (read, write) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);

        using var response = await RequestTokenAsync(clientId, $"{read} {write}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_scope");
        Assert.Empty(await GetAuditTypesAsync(clientId));
    }

    [Theory]
    [InlineData("openid")]
    [InlineData("authora.admin")]
    public async Task SystemScope_IsRejected_EvenIfAllowed(string systemScope)
    {
        // System Scopes need a User: openid for ID Tokens and UserInfo, authora.admin for the User's Permissions.
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, systemScope);

        using var response = await RequestTokenAsync(clientId, systemScope);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_scope");
    }

    [Fact]
    public async Task ClientWithoutTheGrantType_IsRejected()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.AuthorizationCode | GrantTypes.RefreshToken, read);

        using var response = await RequestTokenAsync(clientId, read);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "unauthorized_client");
    }

    [Fact]
    public async Task PublicClient_IsRejected()
    {
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "client_credentials"),
            new("client_id", clientId.ToString())
        ]);
        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "unauthorized_client");
    }

    [Fact]
    public async Task WrongSecret_IsRejected()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);

        using var response = await RequestTokenAsync(clientId, read, secret: "wrong-secret-0123456789012345678");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
    }

    [Fact]
    public async Task Introspection_ReturnsTheClientAsSubject()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);
        var resourceServerId = await CreateResourceServerAsync(read);
        var token = await GetTokenAsync(clientId, read);

        var introspection = await IntrospectAsync(resourceServerId, token);

        Assert.True(introspection.GetProperty("active").GetBoolean());
        Assert.Equal(clientId.ToString(), introspection.GetProperty("sub").GetString());
        Assert.Equal(clientId.ToString(), introspection.GetProperty("client_id").GetString());
        Assert.Equal(read, introspection.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task RevokedToken_IsInactive()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);
        var token = await GetTokenAsync(clientId, read);

        using var revoke = new HttpRequestMessage(HttpMethod.Post, "/oauth/revoke")
        {
            Content = new FormUrlEncodedContent([new("token", token)])
        };
        revoke.Headers.Authorization = BasicAuthorization(clientId, Secret);
        using var revoked = await _client.SendAsync(revoke, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        Assert.False((await IntrospectAsync(clientId, token)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task DisabledClient_TokenIsInactive()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);
        var resourceServerId = await CreateResourceServerAsync(read);
        var token = await GetTokenAsync(clientId, read);

        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        using var disable = new HttpRequestMessage(HttpMethod.Post, $"/api/clients/{clientId}/disable");
        disable.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var disabled = await _client.SendAsync(disable, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);

        Assert.False((await IntrospectAsync(resourceServerId, token)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task UserInfo_RejectsTheToken()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);
        var token = await GetTokenAsync(clientId, read);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminApi_RejectsTheToken()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateClientAsync(GrantTypes.ClientCredentials, read);
        var token = await GetTokenAsync(clientId, read);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_AllowsTheGrantTypeOnlyForConfidentialClients()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        Assert.Equal(HttpStatusCode.Created, await RegisterAsync(adminToken, "Confidential"));
        Assert.Equal(HttpStatusCode.BadRequest, await RegisterAsync(adminToken, "Public"));
    }

    private async Task<HttpStatusCode> RegisterAsync(string adminToken, string type)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/clients")
        {
            Content = JsonContent.Create(new { name = "Backend service", type, grantTypes = new[] { "client_credentials" } })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private async Task<(string Read, string Write)> CreateScopesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var prefix = $"orders{Guid.NewGuid():N}";
        context.Scopes.AddRange(Scope.Create($"{prefix}.read", "Read orders"), Scope.Create($"{prefix}.write", "Write orders"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return ($"{prefix}.read", $"{prefix}.write");
    }

    private async Task<List<string>> CreateLongScopesAsync(int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var prefix = Guid.NewGuid().ToString("N");
        var names = Enumerable.Range(0, count)
            .Select(index => $"{prefix}.{index:D3}.".PadRight(Scope.NameMaxLength, 'x'))
            .ToList();
        context.Scopes.AddRange(names.Select(name => Scope.Create(name, "Long Scope")));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return names;
    }

    private Task<Guid> CreateClientAsync(GrantTypes grantTypes, params string[] allowedScopes)
    {
        return CreateClientAsync(grantTypes, servedScope: null, allowedScopes);
    }

    private Task<Guid> CreateResourceServerAsync(string servedScope)
    {
        return CreateClientAsync(GrantTypes.AuthorizationCode, servedScope, []);
    }

    private async Task<Guid> CreateClientAsync(GrantTypes grantTypes, string? servedScope, string[] allowedScopes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var client = Client.Create("Backend service", ClientType.Confidential);
        client.SetAllowedGrantTypes(grantTypes);
        client.AddSecret(secretHasher.Hash(Secret), DateTime.UtcNow, expiresAt: null);

        foreach (var allowed in await context.Scopes.Where(allowed => allowedScopes.Contains(allowed.Name)).ToListAsync(TestContext.Current.CancellationToken))
        {
            client.AllowScope(allowed);
        }

        if (servedScope is not null)
        {
            client.ServeScope(await context.Scopes.SingleAsync(served => served.Name == servedScope, TestContext.Current.CancellationToken));
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client.Id;
    }

    private async Task<HttpResponseMessage> RequestTokenAsync(Guid clientId, string? scope, string secret = Secret)
    {
        var form = new List<KeyValuePair<string, string>> { new("grant_type", "client_credentials") };

        if (scope is not null)
        {
            form.Add(new("scope", scope));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token") { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = BasicAuthorization(clientId, secret);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> GetTokenAsync(Guid clientId, string scope)
    {
        using var response = await RequestTokenAsync(clientId, scope);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return tokens.GetProperty("access_token").GetString()!;
    }

    private async Task<JsonElement> IntrospectAsync(Guid callerId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/introspect")
        {
            Content = new FormUrlEncodedContent([new("token", token)])
        };
        request.Headers.Authorization = BasicAuthorization(callerId, Secret);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static AuthenticationHeaderValue BasicAuthorization(Guid clientId, string secret)
    {
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}")));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
    {
        Assert.Equal(status, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error, body.GetProperty("error").GetString());
    }

    private async Task<List<string>> GetAuditTypesAsync(Guid clientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
