// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Api.AdminUi;
using Authora.Api.Authentication;
using Authora.Api.Controllers;
using Authora.Api.Security;
using Authora.Core.Application.AdminUi;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Users.GetCurrent;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the admin interface is served securely, signs in through its backend for frontend with the seeded
///     first-party Public Client, and calls the admin API with a cookie that no script can read (US-033, TD-012,
///     TD-014).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AdminUiTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string RedirectUri = $"{AuthoraApiFactory.Issuer}/admin/callback";

    private const string AdminHeader = "X-Authora-Admin";

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
    public async Task AdminUi_WithoutTrailingSlash_RedirectsToAdminUi()
    {
        using var response = await _client.GetAsync("/admin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.EndsWith("/admin/", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeededClient_IsFirstPartyPublicClientWithServerSideRedirectUri()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients
            .Include(client => client.AllowedScopes)
            .SingleAsync(client => client.RedirectUris.Any(uri => uri.Value == RedirectUri), TestContext.Current.CancellationToken);

        Assert.Equal(AdminUiOptions.ClientName, client.Name);
        Assert.Equal(ClientType.Public, client.Type);
        Assert.Equal(ClientStatus.Enabled, client.Status);
        Assert.True(client.IsFirstParty);
        Assert.Equal(AdminApiScope.Name, Assert.Single(client.AllowedScopes).Name);
        Assert.Equal(RedirectUri, Assert.Single(client.RedirectUris).Value);
    }

    [Fact]
    public async Task SignIn_KeepsTokensInHttpOnlyCookie_AndGrantsAccessToAdminApi()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var sessionToken = await factory.CreateSessionAsync(administratorId);

        using var start = await _client.GetAsync("/admin/signin?returnTo=%23%2Fusers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var signInCookie = GetSetCookie(start, AdminUiCookies.SignInName);
        Assert.Contains("httponly", signInCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", signInCookie, StringComparison.OrdinalIgnoreCase);

        // The verifier never leaves the server, only its challenge.
        var location = start.Headers.Location!.OriginalString;
        var authorize = QueryHelpers.ParseQuery(location[location.IndexOf('?', StringComparison.Ordinal)..]);
        Assert.StartsWith("/oauth/authorize?", location, StringComparison.Ordinal);
        Assert.Equal(RedirectUri, authorize["redirect_uri"].Single());
        Assert.Equal("S256", authorize["code_challenge_method"].Single());
        Assert.False(authorize.ContainsKey("code_verifier"));

        // First-party: no consent screen (TD-012).
        using var authorization = await SendAsync(HttpMethod.Get, location, $"{SessionCookie.Name}={sessionToken}");
        Assert.Equal(HttpStatusCode.SeeOther, authorization.StatusCode);
        Assert.StartsWith(RedirectUri, authorization.Headers.Location!.ToString(), StringComparison.Ordinal);

        using var callback = await SendAsync(
            HttpMethod.Get,
            authorization.Headers.Location.PathAndQuery,
            $"{CookiePair(signInCookie)}; {SessionCookie.Name}={sessionToken}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/admin/#/users", callback.Headers.Location!.OriginalString);

        var tokensCookie = GetSetCookie(callback, AdminUiCookies.TokensName);
        Assert.Contains("httponly", tokensCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", tokensCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", tokensCookie, StringComparison.OrdinalIgnoreCase);

        var me = await GetMeAsync(CookiePair(tokensCookie));

        Assert.Equal(administratorId, me.Id);
        Assert.Equal(PermissionNames.All.Order(StringComparer.Ordinal), me.Permissions);
    }

    [Fact]
    public async Task Cookie_WithoutAdminHeader_IsIgnored()
    {
        var tokensCookie = await SignInAsync();

        using var response = await SendAsync(HttpMethod.Get, "/api/me", tokensCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_FromCrossSiteRequest_IsIgnored()
    {
        var tokensCookie = await SignInAsync();

        using var response = await SendAsync(HttpMethod.Get, "/api/me", tokensCookie, request =>
        {
            request.Headers.Add(AdminHeader, "1");
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_ThatWasTamperedWith_IsIgnored()
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/me", $"{AdminUiCookies.TokensName}=not-encrypted-by-authora", AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_WithForeignState_DoesNotRedeemTheCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateAdministratorAsync());

        using var start = await _client.GetAsync("/admin/signin", TestContext.Current.CancellationToken);
        var signInCookie = $"{CookiePair(GetSetCookie(start, AdminUiCookies.SignInName))}; {SessionCookie.Name}={sessionToken}";

        using var authorization = await SendAsync(HttpMethod.Get, start.Headers.Location!.OriginalString, $"{SessionCookie.Name}={sessionToken}");
        var response = QueryHelpers.ParseQuery(authorization.Headers.Location!.Query);
        var code = response["code"].Single();

        // E.g. a script that started its own authorization request: its state belongs to no sign-in of the server.
        using (var foreign = await SendAsync(HttpMethod.Get, $"/admin/callback?code={code}&state=foreign&iss={Uri.EscapeDataString(AuthoraApiFactory.Issuer)}", signInCookie))
        {
            Assert.Equal(HttpStatusCode.Redirect, foreign.StatusCode);
            Assert.Equal($"/admin/?error={AdminUiController.InvalidResponseError}", foreign.Headers.Location!.OriginalString);
            Assert.False(foreign.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(cookie => cookie.StartsWith($"{AdminUiCookies.TokensName}=", StringComparison.Ordinal)));
        }

        // The code is still unused, so the first callback did not redeem it.
        using var own = await SendAsync(HttpMethod.Get, authorization.Headers.Location.PathAndQuery, signInCookie);

        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
        Assert.Equal("/admin/", own.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Callback_WithoutSignInCookie_IsRejected()
    {
        using var response = await _client.GetAsync($"/admin/callback?code=abc&state=abc&iss={Uri.EscapeDataString(AuthoraApiFactory.Issuer)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/admin/?error={AdminUiController.InvalidResponseError}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Callback_WithoutSession_DoesNotRedeemTheCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateAdministratorAsync());

        using var start = await _client.GetAsync("/admin/signin", TestContext.Current.CancellationToken);
        using var authorization = await SendAsync(HttpMethod.Get, start.Headers.Location!.OriginalString, $"{SessionCookie.Name}={sessionToken}");

        // The tokens are bound to the Session of the browser, so a callback without one gets none.
        using var callback = await SendAsync(HttpMethod.Get, authorization.Headers.Location!.PathAndQuery, CookiePair(GetSetCookie(start, AdminUiCookies.SignInName)));

        Assert.Equal($"/admin/?error={AdminUiController.SignInFailedError}", callback.Headers.Location!.OriginalString);
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(cookie => cookie.StartsWith($"{AdminUiCookies.TokensName}=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Callback_AfterAccountSwitch_RevokesTheTokensOfTheCode()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var sessionToken = await factory.CreateSessionAsync(administratorId);
        var otherSessionToken = await factory.CreateSessionAsync(await factory.CreateAdministratorAsync());

        using var start = await _client.GetAsync("/admin/signin", TestContext.Current.CancellationToken);
        using var authorization = await SendAsync(HttpMethod.Get, start.Headers.Location!.OriginalString, $"{SessionCookie.Name}={sessionToken}");

        // Another tab signed in as another User before the callback, so the browser now sends that User's Session.
        using var callback = await SendAsync(
            HttpMethod.Get,
            authorization.Headers.Location!.PathAndQuery,
            $"{CookiePair(GetSetCookie(start, AdminUiCookies.SignInName))}; {SessionCookie.Name}={otherSessionToken}");

        Assert.Equal($"/admin/?error={AdminUiController.SignInFailedError}", callback.Headers.Location!.OriginalString);
        Assert.False(callback.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(cookie => cookie.StartsWith($"{AdminUiCookies.TokensName}=", StringComparison.Ordinal)));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var grant = await context.Grants.SingleAsync(grant => grant.UserId == administratorId, TestContext.Current.CancellationToken);

        Assert.NotNull(grant.RevokedAt);
    }

    [Fact]
    public async Task SignIn_WithForeignReturnTo_ReturnsToStartPage()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateAdministratorAsync());

        using var start = await _client.GetAsync("/admin/signin?returnTo=https%3A%2F%2Fevil.example.com", TestContext.Current.CancellationToken);
        using var authorization = await SendAsync(HttpMethod.Get, start.Headers.Location!.OriginalString, $"{SessionCookie.Name}={sessionToken}");
        using var callback = await SendAsync(
            HttpMethod.Get,
            authorization.Headers.Location!.PathAndQuery,
            $"{CookiePair(GetSetCookie(start, AdminUiCookies.SignInName))}; {SessionCookie.Name}={sessionToken}");

        Assert.Equal("/admin/", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Refresh_RightAfterSignIn_IsSkipped()
    {
        var tokensCookie = await SignInAsync();

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", tokensCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Refresh_OfOlderTokens_IssuesNewTokens()
    {
        var tokensCookie = await SignInAsync();
        var olderCookie = Age(tokensCookie);

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", olderCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refreshedCookie = CookiePair(GetSetCookie(response, AdminUiCookies.TokensName));
        Assert.NotEqual(tokensCookie, refreshedCookie);
        await GetMeAsync(refreshedCookie);

        // The refreshed tokens stay bound to the Session the User signed in with.
        Assert.Equal(ReadTokens(tokensCookie).SessionId, ReadTokens(refreshedCookie).SessionId);

        // The Refresh Token was rotated: the old one counts as reuse and revokes the Grant.
        using var reuse = await SendAsync(HttpMethod.Post, "/admin/session/refresh", olderCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterTheSessionEnded_RevokesTheGrantAndIsUnauthorized()
    {
        var tokensCookie = await SignInAsync();

        // E.g. signed out of Authora in another tab, expired or idle: the Session is no longer valid.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var sessionId = ReadTokens(tokensCookie).SessionId;
            var session = await context.Sessions.SingleAsync(session => session.Id == sessionId, TestContext.Current.CancellationToken);

            session.Revoke(DateTime.UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", Age(tokensCookie), AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", GetSetCookie(response, AdminUiCookies.TokensName), StringComparison.OrdinalIgnoreCase);

        // The Grant is revoked, so the Access Token still in the old cookie stops working too.
        using var me = await SendAsync(HttpMethod.Get, "/api/me", tokensCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnotherSession_IsUnauthorized()
    {
        var tokensCookie = await SignInAsync();
        var userId = (await GetMeAsync(tokensCookie)).Id;

        // The same User signed in again, so the browser now sends a Session the tokens are not bound to.
        var otherSession = $"{SessionCookie.Name}={await factory.CreateSessionAsync(userId)}";
        var cookie = $"{Age(tokensCookie).Split("; ")[0]}; {otherSession}";

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", cookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutAdminHeader_IsBadRequest()
    {
        var tokensCookie = await SignInAsync();

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", tokensCookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_IsUnauthorized()
    {
        using var response = await SendAsync(HttpMethod.Post, "/admin/session/refresh", cookie: null, AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignOut_RevokesTheTokensAndDeletesTheCookie()
    {
        var tokensCookie = await SignInAsync();

        using var response = await SendAsync(HttpMethod.Post, "/admin/session/signout", tokensCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", GetSetCookie(response, AdminUiCookies.TokensName), StringComparison.OrdinalIgnoreCase);

        using var me = await SendAsync(HttpMethod.Get, "/api/me", tokensCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsOnlyThePermissionsOfTheUser()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Read, PermissionNames.Groups.Read);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var me = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(userId, me!.Id);
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
        using var signIn = await client.GetAsync("/admin/signin", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, script.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, signIn.StatusCode);
    }

    private static void AddAdminHeader(HttpRequestMessage request)
    {
        request.Headers.Add(AdminHeader, "1");
    }

    private static string GetSetCookie(HttpResponseMessage response, string name)
    {
        return response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie)
    {
        return setCookie.Split(';')[0];
    }

    /// <summary>
    ///     Signs in an administrator through the backend for frontend.
    /// </summary>
    /// <returns>
    ///     The cookie with the tokens and the Session cookie the browser sends along, as <c>name=value; name=value</c>.
    /// </returns>
    private async Task<string> SignInAsync()
    {
        var sessionCookie = $"{SessionCookie.Name}={await factory.CreateSessionAsync(await factory.CreateAdministratorAsync())}";

        using var start = await _client.GetAsync("/admin/signin", TestContext.Current.CancellationToken);
        using var authorization = await SendAsync(HttpMethod.Get, start.Headers.Location!.OriginalString, sessionCookie);
        using var callback = await SendAsync(
            HttpMethod.Get,
            authorization.Headers.Location!.PathAndQuery,
            $"{CookiePair(GetSetCookie(start, AdminUiCookies.SignInName))}; {sessionCookie}");

        return $"{CookiePair(GetSetCookie(callback, AdminUiCookies.TokensName))}; {sessionCookie}";
    }

    /// <summary>
    ///     Writes the tokens of a cookie again as if they were issued a minute ago, so a refresh is not skipped. The
    ///     other cookies are kept.
    /// </summary>
    private string Age(string cookie)
    {
        var cookies = factory.Services.GetRequiredService<AdminUiCookies>();
        var tokens = ReadTokens(cookie);

        var context = new DefaultHttpContext();
        cookies.AppendTokens(context.Response, tokens with { IssuedAt = tokens.IssuedAt.AddMinutes(-1) });

        var others = cookie.Split("; ").Where(pair => !pair.StartsWith($"{AdminUiCookies.TokensName}=", StringComparison.Ordinal));

        return string.Join("; ", others.Prepend(CookiePair(context.Response.Headers.SetCookie.Single()!)));
    }

    private AdminUiTokens ReadTokens(string tokensCookie)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = tokensCookie;
        Assert.True(factory.Services.GetRequiredService<AdminUiCookies>().TryReadTokens(request, out var tokens));

        return tokens;
    }

    private async Task<GetCurrentUserResponse> GetMeAsync(string tokensCookie)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/me", tokensCookie, AddAdminHeader);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? cookie,
        Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        configure?.Invoke(request);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
