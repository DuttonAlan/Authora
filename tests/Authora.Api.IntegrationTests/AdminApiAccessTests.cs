// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

using Authora.Api.Authentication;
using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks at request level who can call the admin API (US-032).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AdminApiAccessTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // Any admin endpoint works, they all share the same policies.
    private const string Endpoint = "/api/users";
    private const string RequiredPermission = PermissionNames.Users.Read;

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false });

    [Fact]
    public async Task RequestWithoutCredentials_IsUnauthorized()
    {
        using var response = await _client.GetAsync(Endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task RequestWithOnlySessionCookie_IsUnauthorized()
    {
        // An administrator who is signed in through the browser, so the cookie alone must not be enough.
        var userId = await factory.CreateAdministratorAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithUnknownToken_IsUnauthorized()
    {
        using var response = await SendAsync("not-a-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task RequestWithExpiredToken_IsUnauthorized()
    {
        var userId = await factory.CreateAdministratorAsync();
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name], DateTime.UtcNow.AddHours(-2));

        using var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer error=\"invalid_token\"", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task RequestWithoutAdminScope_IsForbiddenWithInsufficientScope()
    {
        var userId = await factory.CreateAdministratorAsync();
        var token = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        using var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            $"Bearer error=\"insufficient_scope\", scope=\"{AdminApiScope.Name}\"",
            response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task RequestWithoutPermission_IsForbidden()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Roles.Read);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // A new token would not help, so the Client is not asked for another Scope.
        Assert.Empty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task RequestWithPermission_Succeeds()
    {
        var userId = await factory.CreateUserAsync(RequiredPermission);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestOfAdministrator_Succeeds()
    {
        var userId = await factory.CreateAdministratorAsync();
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var response = await SendAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RoleRemovedBetweenRequests_TakesEffectOnNextRequest()
    {
        var userId = await factory.CreateUserAsync(RequiredPermission);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using (var response = await SendAsync(token))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await factory.RemoveRolesAsync(userId);

        using (var response = await SendAsync(token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
