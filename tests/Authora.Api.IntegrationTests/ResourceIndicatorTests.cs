// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Authora.Api.Authentication;
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
///     Checks audience-restricted Access Tokens (RFC 8707): the <c>resource</c> parameter at the authorization and
///     token endpoints, downscoping, Scopes cut to the audience, introspection only for the token's Client and its
///     audience, the audience of tokens requested without <c>resource</c>, and Authora checking that it is in the
///     audience of tokens for the admin API and UserInfo.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ResourceIndicatorTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Secret = "resource-indicator-test-secret-0123456789";

    private static readonly string AuthoraResourceIdentifier = $"{AuthoraApiFactory.Issuer}/api";

    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task ClientCredentials_WithResource_CutsScopesToTheApi()
    {
        var (read, write) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync(read);
        await CreateApiAsync(write);
        var clientId = await CreateServiceAsync(read, write);

        // Without scope, the token gets every Scope the Client may have at the API.
        using var response = await RequestClientCredentialsAsync(clientId, scope: null, orders);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(read, tokens.GetProperty("scope").GetString());

        var token = tokens.GetProperty("access_token").GetString()!;
        var byApi = await IntrospectAsync(ordersId, token);

        Assert.True(byApi.GetProperty("active").GetBoolean());
        Assert.Equal(read, byApi.GetProperty("scope").GetString());
        Assert.Equal([orders], byApi.GetProperty("aud").EnumerateArray().Select(value => value.GetString()));

        var details = await GetAuditDetailsAsync(clientId, AuditEventTypes.Clients.TokenIssued);
        Assert.Contains($"audience '{ordersId}'", details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Introspection_ByTheClient_TheAudienceAndAForeignApi()
    {
        var (read, write) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync(read);
        var (_, billingId) = await CreateApiAsync(read, write);
        var clientId = await CreateServiceAsync(read, write);

        var token = await GetClientCredentialsTokenAsync(clientId, $"{read} {write}", orders);

        // The Client the token was issued to sees the cut Scopes, like the token response.
        var byClient = await IntrospectAsync(clientId, token);
        Assert.True(byClient.GetProperty("active").GetBoolean());
        Assert.Equal(read, byClient.GetProperty("scope").GetString());

        var byAudience = await IntrospectAsync(ordersId, token);
        Assert.True(byAudience.GetProperty("active").GetBoolean());

        // Serves the Scope, but the token was not requested for it.
        var byForeignApi = await IntrospectAsync(billingId, token);
        Assert.False(byForeignApi.GetProperty("active").GetBoolean());
        Assert.False(byForeignApi.TryGetProperty("aud", out _));
    }

    [Theory]
    [InlineData("https://unknown.example.com")]
    [InlineData("https://api.example.com/#fragment")]
    [InlineData("not-a-uri")]
    public async Task ClientCredentials_InvalidResource_IsInvalidTarget(string resource)
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateServiceAsync(read);

        using var response = await RequestClientCredentialsAsync(clientId, read, resource);

        await AssertErrorAsync(response, "invalid_target");
    }

    [Fact]
    public async Task ClientCredentials_ResourceServingNoRequestedScope_IsInvalidTarget()
    {
        var (read, write) = await CreateScopesAsync();
        var (billing, _) = await CreateApiAsync(write);
        var clientId = await CreateServiceAsync(read, write);

        using var response = await RequestClientCredentialsAsync(clientId, read, billing);

        await AssertErrorAsync(response, "invalid_target");
    }

    [Fact]
    public async Task ClientCredentials_DisabledApi_IsInvalidTarget()
    {
        var (read, _) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync(read);
        var clientId = await CreateServiceAsync(read);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var api = await context.Clients.SingleAsync(client => client.Id == ordersId, TestContext.Current.CancellationToken);
            api.Disable();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await RequestClientCredentialsAsync(clientId, read, orders);

        await AssertErrorAsync(response, "invalid_target");
    }

    [Fact]
    public async Task WithoutResource_TokenIsMeantForEveryApiServingOneOfItsScopes()
    {
        var (read, write) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync(read);
        var (billing, billingId) = await CreateApiAsync(write);
        var clientId = await CreateServiceAsync(read, write);

        var token = await GetClientCredentialsTokenAsync(clientId, $"{read} {write}");

        var byOrders = await IntrospectAsync(ordersId, token);
        Assert.True(byOrders.GetProperty("active").GetBoolean());
        Assert.Equal(read, byOrders.GetProperty("scope").GetString());
        Assert.Equal(
            new[] { orders, billing }.Order(StringComparer.Ordinal),
            byOrders.GetProperty("aud").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));

        var byBilling = await IntrospectAsync(billingId, token);
        Assert.True(byBilling.GetProperty("active").GetBoolean());
        Assert.Equal(write, byBilling.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task ApiThatServesAScopeLater_DoesNotSeeEarlierTokens()
    {
        var (read, _) = await CreateScopesAsync();
        var clientId = await CreateServiceAsync(read);

        var token = await GetClientCredentialsTokenAsync(clientId, read);
        var (_, ordersId) = await CreateApiAsync(read);

        var introspection = await IntrospectAsync(ordersId, token);
        Assert.False(introspection.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task RequiresResourceIndicator_ApiSeesOnlyTokensThatNameIt()
    {
        var (read, _) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync([read], requiresResourceIndicator: true);
        var clientId = await CreateServiceAsync(read);

        var withoutResource = await GetClientCredentialsTokenAsync(clientId, read);
        Assert.False((await IntrospectAsync(ordersId, withoutResource)).GetProperty("active").GetBoolean());

        var withResource = await GetClientCredentialsTokenAsync(clientId, read, orders);
        Assert.True((await IntrospectAsync(ordersId, withResource)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task AuthorizationRequest_ScopeNotServedByTheResources_IsInvalidTarget()
    {
        var (read, write) = await CreateScopesAsync();
        var (orders, _) = await CreateApiAsync(read);
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync("openid", read, write);
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());

        using var response = await AuthorizeAsync(clientId, redirectUri, $"openid {read} {write}", [orders, AuthoraResourceIdentifier], sessionToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("invalid_target", query["error"].Single());
        Assert.Equal("test-state", query["state"].Single());
    }

    [Fact]
    public async Task AuthorizationRequest_UnknownResource_IsInvalidTarget()
    {
        var (read, _) = await CreateScopesAsync();
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync(read);
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());

        using var response = await AuthorizeAsync(clientId, redirectUri, read, ["https://unknown.example.com"], sessionToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("invalid_target", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].Single());
    }

    [Fact]
    public async Task AuthorizationCode_DownscopingAtTheTokenEndpointAndOnRefresh()
    {
        var (read, write) = await CreateScopesAsync();
        var (orders, ordersId) = await CreateApiAsync(read);
        var (billing, _) = await CreateApiAsync(write);
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync("openid", read, write);
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());

        var code = await GetCodeAsync(clientId, redirectUri, $"openid {read}", [orders, AuthoraResourceIdentifier], sessionToken);

        // Only the API: the token loses openid, but the ID Token is still issued for the approved openid.
        using var exchange = await RequestTokenAsync(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", AuthorizationCodeFlow.CodeVerifier),
            new("resource", orders)
        ]);

        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        var tokens = await exchange.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(read, tokens.GetProperty("scope").GetString());
        Assert.True(tokens.TryGetProperty("id_token", out _));

        var accessToken = tokens.GetProperty("access_token").GetString()!;
        Assert.True((await IntrospectAsync(ordersId, accessToken)).GetProperty("active").GetBoolean());

        // Not meant for Authora, so UserInfo rejects it.
        using (var userInfo = await GetUserInfoAsync(accessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
        }

        var refreshToken = tokens.GetProperty("refresh_token").GetString()!;

        // A resource that was not approved cannot be added on refresh.
        using (var widened = await RequestTokenAsync(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", refreshToken),
            new("resource", billing)
        ]))
        {
            await AssertErrorAsync(widened, "invalid_target");
        }

        // The rejected request did not use up the Refresh Token. Without resource, the token gets all approved ones.
        using var refreshed = await RequestTokenAsync(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", refreshToken)
        ]);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var refreshedTokens = await refreshed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal($"openid {read}", refreshedTokens.GetProperty("scope").GetString());

        var refreshedAccessToken = refreshedTokens.GetProperty("access_token").GetString()!;

        using (var userInfo = await GetUserInfoAsync(refreshedAccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        }

        var introspection = await IntrospectAsync(ordersId, refreshedAccessToken);
        Assert.Equal(
            new[] { orders, AuthoraResourceIdentifier }.Order(StringComparer.Ordinal),
            introspection.GetProperty("aud").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));

        // Downscoping on refresh: only Authora.
        using var downscoped = await RequestTokenAsync(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", refreshedTokens.GetProperty("refresh_token").GetString()!),
            new("resource", AuthoraResourceIdentifier)
        ]);

        Assert.Equal(HttpStatusCode.OK, downscoped.StatusCode);

        var downscopedTokens = await downscoped.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("openid", downscopedTokens.GetProperty("scope").GetString());
        Assert.False((await IntrospectAsync(ordersId, downscopedTokens.GetProperty("access_token").GetString()!)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task AuthorizationCode_WithoutResource_KeepsWorkingForServingApisAndAuthora()
    {
        var (read, _) = await CreateScopesAsync();
        var (_, ordersId) = await CreateApiAsync(read);
        var (clientId, redirectUri) = await CreateFirstPartyClientAsync("openid", read);
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());

        var code = await GetCodeAsync(clientId, redirectUri, $"openid {read}", [], sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        Assert.Equal($"openid {read}", tokens.GetProperty("scope").GetString());

        var accessToken = tokens.GetProperty("access_token").GetString()!;
        var introspection = await IntrospectAsync(ordersId, accessToken);

        Assert.True(introspection.GetProperty("active").GetBoolean());
        Assert.Equal(read, introspection.GetProperty("scope").GetString());

        using var userInfo = await GetUserInfoAsync(accessToken);
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
    }

    [Fact]
    public async Task AdminApi_RejectsTokenWithoutAuthoraInItsAudience()
    {
        var administratorId = await factory.CreateAdministratorAsync();

        var withoutAuthora = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name], audience: []);

        using (var rejected = await SendAdminAsync(HttpMethod.Get, "/api/clients", withoutAuthora))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            Assert.Contains("invalid_token", rejected.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        }

        var withAuthora = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);

        using var accepted = await SendAdminAsync(HttpMethod.Get, "/api/clients", withAuthora);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task AdminApi_SetsAndClearsTheResourceIdentifier()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var clientId = await CreateServiceAsync();
        var identifier = $"https://{Guid.NewGuid():N}.example.com/api";

        using (var set = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{clientId}", adminToken, new { resourceIdentifier = identifier, requiresResourceIndicator = true }))
        {
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        var client = await GetClientAsync(clientId, adminToken);
        Assert.Equal(identifier, client.GetProperty("resourceIdentifier").GetString());
        Assert.True(client.GetProperty("requiresResourceIndicator").GetBoolean());

        var details = await GetAuditDetailsAsync(clientId, AuditEventTypes.Clients.Updated);
        Assert.Contains($"Resource Identifier '{identifier}', resource parameter required", details, StringComparison.Ordinal);

        // Unique among all Clients.
        var otherId = await CreateServiceAsync();

        using (var duplicate = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{otherId}", adminToken, new { resourceIdentifier = identifier }))
        {
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }

        // An empty string removes it, and the setting with it.
        using (var cleared = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{clientId}", adminToken, new { resourceIdentifier = string.Empty }))
        {
            Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        }

        client = await GetClientAsync(clientId, adminToken);
        Assert.Equal(JsonValueKind.Null, client.GetProperty("resourceIdentifier").ValueKind);
        Assert.False(client.GetProperty("requiresResourceIndicator").GetBoolean());
    }

    [Theory]
    [InlineData("http://api.example.com")]
    [InlineData("https://api.example.com/?query=1")]
    [InlineData("https://api.example.com/#fragment")]
    [InlineData("/relative")]
    [InlineData(AuthoraApiFactory.Issuer + "/api")]
    public async Task AdminApi_InvalidResourceIdentifier_IsBadRequest(string identifier)
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var clientId = await CreateServiceAsync();

        using var response = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{clientId}", adminToken, new { resourceIdentifier = identifier });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminApi_PublicClientOrMissingIdentifier_IsBadRequest()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (publicClientId, _) = await factory.CreatePublicClientAsync();

        using (var publicClient = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{publicClientId}", adminToken, new { resourceIdentifier = $"https://{Guid.NewGuid():N}.example.com" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, publicClient.StatusCode);
        }

        var clientId = await CreateServiceAsync();

        using var withoutIdentifier = await SendAdminAsync(HttpMethod.Put, $"/api/clients/{clientId}", adminToken, new { requiresResourceIndicator = true });
        Assert.Equal(HttpStatusCode.BadRequest, withoutIdentifier.StatusCode);
    }

    private async Task<(string Read, string Write)> CreateScopesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var prefix = $"res{Guid.NewGuid():N}";
        context.Scopes.AddRange(Scope.Create($"{prefix}.read", "Read"), Scope.Create($"{prefix}.write", "Write"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return ($"{prefix}.read", $"{prefix}.write");
    }

    private Task<(string Identifier, Guid Id)> CreateApiAsync(params string[] servedScopes)
    {
        return CreateApiAsync(servedScopes, requiresResourceIndicator: false);
    }

    private async Task<(string Identifier, Guid Id)> CreateApiAsync(string[] servedScopes, bool requiresResourceIndicator)
    {
        var identifier = $"https://{Guid.NewGuid():N}.example.com/api";
        var id = await CreateConfidentialClientAsync(GrantTypes.AuthorizationCode, [], client =>
        {
            client.SetResourceIdentifier(identifier, allowHttp: false);
            client.SetRequiresResourceIndicator(requiresResourceIndicator);
        }, servedScopes);

        return (identifier, id);
    }

    private Task<Guid> CreateServiceAsync(params string[] allowedScopes)
    {
        return CreateConfidentialClientAsync(GrantTypes.ClientCredentials, allowedScopes, _ => { }, []);
    }

    private async Task<Guid> CreateConfidentialClientAsync(
        GrantTypes grantTypes,
        string[] allowedScopes,
        Action<Client> configure,
        string[] servedScopes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var client = Client.Create("Resource test", ClientType.Confidential);
        client.SetAllowedGrantTypes(grantTypes);
        client.AddSecret(secretHasher.Hash(Secret), DateTime.UtcNow, expiresAt: null);

        foreach (var allowed in await context.Scopes.Where(allowed => allowedScopes.Contains(allowed.Name)).ToListAsync(TestContext.Current.CancellationToken))
        {
            client.AllowScope(allowed);
        }

        foreach (var served in await context.Scopes.Where(served => servedScopes.Contains(served.Name)).ToListAsync(TestContext.Current.CancellationToken))
        {
            client.ServeScope(served);
        }

        configure(client);

        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client.Id;
    }

    private async Task<(Guid ClientId, string RedirectUri)> CreateFirstPartyClientAsync(params string[] scopeNames)
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync(scopeNames);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.SetFirstParty(true);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (clientId, redirectUri);
    }

    private async Task<HttpResponseMessage> AuthorizeAsync(
        Guid clientId,
        string redirectUri,
        string scope,
        IReadOnlyList<string> resources,
        string sessionToken)
    {
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, scope)
            .Select(parameter => new KeyValuePair<string, string?>(parameter.Key, parameter.Value))
            .Concat(resources.Select(resource => new KeyValuePair<string, string?>("resource", resource)));

        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("/oauth/authorize", parameters));
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> GetCodeAsync(Guid clientId, string redirectUri, string scope, IReadOnlyList<string> resources, string sessionToken)
    {
        using var response = await AuthorizeAsync(clientId, redirectUri, scope, resources, sessionToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        return QueryHelpers.ParseQuery(response.Headers.Location!.Query)["code"].Single()!;
    }

    private async Task<HttpResponseMessage> RequestTokenAsync(IEnumerable<KeyValuePair<string, string>> form, Guid? basicClientId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token") { Content = new FormUrlEncodedContent(form) };

        if (basicClientId is { } id)
        {
            request.Headers.Authorization = BasicAuthorization(id);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> RequestClientCredentialsAsync(Guid clientId, string? scope, params string[] resources)
    {
        var form = new List<KeyValuePair<string, string>> { new("grant_type", "client_credentials") };

        if (scope is not null)
        {
            form.Add(new("scope", scope));
        }

        form.AddRange(resources.Select(resource => new KeyValuePair<string, string>("resource", resource)));

        return RequestTokenAsync(form, clientId);
    }

    private async Task<string> GetClientCredentialsTokenAsync(Guid clientId, string scope, params string[] resources)
    {
        using var response = await RequestClientCredentialsAsync(clientId, scope, resources);
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
        request.Headers.Authorization = BasicAuthorization(callerId);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetUserInfoAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAdminAsync(HttpMethod method, string path, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetClientAsync(Guid clientId, string adminToken)
    {
        using var response = await SendAdminAsync(HttpMethod.Get, $"/api/clients/{clientId}", adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<string> GetAuditDetailsAsync(Guid clientId, string type)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString() && auditEvent.Type == type)
            .OrderByDescending(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Details!)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private static AuthenticationHeaderValue BasicAuthorization(Guid clientId)
    {
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{Secret}")));
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error, body.GetProperty("error").GetString());
    }
}
