// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Api.Security;
using Authora.Core.Application.AdminUi;
using Authora.Core.Application.AdminUi.GetSettings;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Users.GetCurrent;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the admin interface is served securely and signs in through Authora's own Authorization Code flow
///     with its seeded Public Client, then uses the admin API like any other Client (US-033).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AdminUiTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string RedirectUri = $"{AuthoraApiFactory.Issuer}/admin/";

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task AdminUi_IsServedWithStrictContentSecurityPolicy()
    {
        using var response = await _client.GetAsync("/admin/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SecurityHeadersMiddleware.AdminUiContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task AdminUi_WithoutTrailingSlash_RedirectsToRedirectUri()
    {
        using var response = await _client.GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.EndsWith("/admin/", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_NameTheSeededPublicClient()
    {
        var settings = await _client.GetFromJsonAsync<AdminUiSettingsResponse>("/admin/settings", TestContext.Current.CancellationToken);

        Assert.NotNull(settings);
        Assert.Equal(AuthoraApiFactory.Issuer, settings.Issuer);
        Assert.Equal(RedirectUri, settings.RedirectUri);
        Assert.Equal(AdminApiScope.Name, settings.Scope);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients
            .Include(client => client.AllowedScopes)
            .SingleAsync(client => client.Id == settings.ClientId, TestContext.Current.CancellationToken);

        Assert.Equal(AdminUiOptions.ClientName, client.Name);
        Assert.Equal(ClientType.Public, client.Type);
        Assert.Equal(ClientStatus.Enabled, client.Status);
        Assert.Equal(AdminApiScope.Name, Assert.Single(client.AllowedScopes).Name);
        Assert.Equal(RedirectUri, Assert.Single(client.RedirectUris).Value);
    }

    [Fact]
    public async Task SignIn_WithAdminUiClient_GrantsAccessToAdminApi()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var sessionToken = await factory.CreateSessionAsync(administratorId);
        var settings = (await _client.GetFromJsonAsync<AdminUiSettingsResponse>("/admin/settings", TestContext.Current.CancellationToken))!;

        var parameters = AuthorizationCodeFlow.Parameters(settings.ClientId, settings.RedirectUri, settings.Scope);
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, settings.ClientId, settings.RedirectUri, code);

        Assert.True(tokens.TryGetProperty("refresh_token", out _));

        var me = await GetMeAsync(tokens.GetProperty("access_token").GetString()!);

        Assert.Equal(administratorId, me.Id);
        Assert.Equal(PermissionNames.All.Order(StringComparer.Ordinal), me.Permissions);
    }

    [Fact]
    public async Task Me_ReturnsOnlyThePermissionsOfTheUser()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Read, PermissionNames.Groups.Read);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        var me = await GetMeAsync(token);

        Assert.Equal(userId, me.Id);
        Assert.Equal([PermissionNames.Groups.Read, PermissionNames.Users.Read], me.Permissions);
    }

    [Fact]
    public async Task Me_WithoutAdminScope_IsForbidden()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Read);
        var token = await factory.IssueAccessTokenAsync(userId, ["openid"]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DisabledAdminUi_IsNotServed()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:AdminUi:Enabled", "false"));
        using var client = disabled.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress });

        using var page = await client.GetAsync("/admin/", TestContext.Current.CancellationToken);
        using var script = await client.GetAsync("/admin/js/app.js", TestContext.Current.CancellationToken);
        using var settings = await client.GetAsync("/admin/settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, script.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, settings.StatusCode);
    }

    private async Task<GetCurrentUserResponse> GetMeAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>(TestContext.Current.CancellationToken))!;
    }
}
