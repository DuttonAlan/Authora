// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the UserInfo endpoint only answers active Access Tokens with <c>openid</c>, and only returns the
///     claims of their Scopes.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class UserInfoTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TokenWithOnlyOpenId_ReturnsOnlySubject()
    {
        var userId = await factory.CreateUserAsync();
        var token = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        using var response = await GetUserInfoAsync(HttpMethod.Get, token);
        var claims = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userId.ToString(), Assert.Single(claims.EnumerateObject(), claim => claim.Name == "sub").Value.GetString());
        Assert.Single(claims.EnumerateObject());
    }

    [Fact]
    public async Task TokenWithProfileAndEmail_ReturnsTheirClaims()
    {
        var userId = await factory.CreateUserAsync();
        var token = await factory.IssueAccessTokenAsync(userId, ["openid", "profile", "email"]);

        using var response = await GetUserInfoAsync(HttpMethod.Post, token);
        var claims = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Test User", claims.GetProperty("name").GetString());
        Assert.Equal("Test", claims.GetProperty("given_name").GetString());
        Assert.Equal("User", claims.GetProperty("family_name").GetString());
        Assert.EndsWith("@example.com", claims.GetProperty("email").GetString(), StringComparison.Ordinal);
        Assert.False(claims.GetProperty("email_verified").GetBoolean());

        // Claims without a value are left out instead of being sent as null.
        Assert.False(claims.TryGetProperty("picture", out _));
    }

    [Fact]
    public async Task TokenWithoutOpenId_IsForbiddenWithInsufficientScope()
    {
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), ["profile"]);

        using var response = await GetUserInfoAsync(HttpMethod.Get, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("error=\"insufficient_scope\"", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownToken_IsUnauthorizedWithInvalidToken()
    {
        using var response = await GetUserInfoAsync(HttpMethod.Get, "not-a-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenOfDisabledClient_IsUnauthorizedWithInvalidToken()
    {
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), ["openid"], clientId: clientId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            (await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken)).Disable();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await GetUserInfoAsync(HttpMethod.Get, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenOfDeactivatedUser_IsUnauthorizedWithInvalidToken()
    {
        var userId = await factory.CreateUserAsync();
        var token = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            (await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Deactivate();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await GetUserInfoAsync(HttpMethod.Get, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestWithoutToken_IsUnauthorizedWithoutErrorCode()
    {
        using var response = await _client.GetAsync("/oauth/userinfo", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task PreflightFromOriginOfClient_AllowsAuthorizationHeader()
    {
        var (_, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var origin = new Uri(redirectUri).GetLeftPart(UriPartial.Authority);

        using var request = new HttpRequestMessage(HttpMethod.Options, "/oauth/userinfo");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private async Task<HttpResponseMessage> GetUserInfoAsync(HttpMethod method, string token)
    {
        using var request = new HttpRequestMessage(method, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
