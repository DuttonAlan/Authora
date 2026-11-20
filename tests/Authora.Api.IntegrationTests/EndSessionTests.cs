// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Authora.Api.Authentication;
using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients.Get;
using Authora.Core.Application.Oidc;
using Authora.Core.Domain.Entities;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the end-session endpoint (OpenID Connect RP-Initiated Logout 1.0) and the Post-logout Redirect URIs of
///     Clients.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class EndSessionTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string State = "logout-state";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task HintOfTheSession_SignsOutAndRedirects()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, DateTime.UtcNow);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = idToken,
            ["post_logout_redirect_uri"] = postLogoutUri,
            ["state"] = State
        });

        AssertRedirectedTo(response, postLogoutUri);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{SessionCookie.Name}=;", StringComparison.Ordinal));
        Assert.True(await IsSessionRevokedAsync(userId));
        Assert.Equal([AuditEventTypes.Account.SignedOutByClient], await GetAuditTypesAsync(userId));
    }

    [Fact]
    public async Task ExpiredHintOfTheSession_IsAccepted()
    {
        // RP-Initiated Logout 1.0, Section 4: Clients sign Users out long after the ID Token expired. ID Tokens live
        // 5 minutes, the test Session 1 hour.
        var signedInAt = DateTime.UtcNow.AddMinutes(-30);
        var (userId, sessionToken, _) = await SignInAsync(signedInAt);
        var (clientId, postLogoutUri) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, signedInAt);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = idToken,
            ["post_logout_redirect_uri"] = postLogoutUri,
            ["state"] = State
        });

        AssertRedirectedTo(response, postLogoutUri);
        Assert.True(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task HintOfAnotherSession_AsksForConfirmation()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt.AddMinutes(-30), DateTime.UtcNow);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = idToken,
            ["post_logout_redirect_uri"] = postLogoutUri
        });

        await AssertConfirmationAsync(response);
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task WithoutHint_AsksForConfirmation_AndSignsOutWhenConfirmed()
    {
        var (userId, sessionToken, _) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync();
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId.ToString(),
            ["post_logout_redirect_uri"] = postLogoutUri,
            ["state"] = State
        };

        using var confirmation = await LogoutAsync(sessionToken, parameters);
        await AssertConfirmationAsync(confirmation);
        Assert.False(await IsSessionRevokedAsync(userId));

        using var response = await ConfirmAsync(confirmation, sessionToken, parameters, "sign-out");

        AssertRedirectedTo(response, postLogoutUri);
        Assert.True(await IsSessionRevokedAsync(userId));
        Assert.Equal([AuditEventTypes.Account.SignedOutByClient], await GetAuditTypesAsync(userId));
    }

    [Fact]
    public async Task Confirmation_StayingSignedIn_KeepsTheSession()
    {
        var (userId, sessionToken, _) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync();
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId.ToString(),
            ["post_logout_redirect_uri"] = postLogoutUri,
            ["state"] = State
        };

        using var confirmation = await LogoutAsync(sessionToken, parameters);
        using var response = await ConfirmAsync(confirmation, sessionToken, parameters, "stay");

        AssertRedirectedTo(response, postLogoutUri);
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task Confirmation_WithoutAntiforgeryToken_IsRejected()
    {
        var (userId, sessionToken, _) = await SignInAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/logout")
        {
            Content = new FormUrlEncodedContent([new("decision", "sign-out")])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task UnregisteredPostLogoutRedirectUri_IsShownAsError()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, _) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, DateTime.UtcNow);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = idToken,
            ["post_logout_redirect_uri"] = "https://attacker.example.com/",
            ["state"] = State
        });

        await AssertErrorAsync(response, "not registered");
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task RedirectUriInsteadOfPostLogoutRedirectUri_IsShownAsError()
    {
        // Post-logout Redirect URIs are registered separately and matched exactly.
        var (_, sessionToken, _) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync();

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["client_id"] = clientId.ToString(),
            ["post_logout_redirect_uri"] = postLogoutUri + "/"
        });

        await AssertErrorAsync(response, "not registered");
    }

    [Fact]
    public async Task PostLogoutRedirectUriWithoutClient_IsShownAsError()
    {
        var (_, sessionToken, _) = await SignInAsync();
        var (_, postLogoutUri) = await CreateClientAsync();

        using var response = await LogoutAsync(sessionToken, new() { ["post_logout_redirect_uri"] = postLogoutUri });

        await AssertErrorAsync(response, "client_id");
    }

    [Fact]
    public async Task ClientIdNotMatchingTheHint_IsShownAsError()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, _) = await CreateClientAsync();
        var (otherClientId, _) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, DateTime.UtcNow);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = idToken,
            ["client_id"] = otherClientId.ToString()
        });

        await AssertErrorAsync(response, "does not match");
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task TamperedHint_IsShownAsError()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, _) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, DateTime.UtcNow);
        var parts = idToken.Split('.');
        var otherUser = await factory.CreateUserAsync();
        var forged = await IssueIdTokenAsync(otherUser, clientId, signedInAt, DateTime.UtcNow);

        // The claims of one token with the signature of another.
        using var response = await LogoutAsync(sessionToken, new()
        {
            ["id_token_hint"] = $"{parts[0]}.{forged.Split('.')[1]}.{parts[2]}"
        });

        await AssertErrorAsync(response, "id_token_hint");
        Assert.False(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task DisabledClient_IsShownAsError()
    {
        var (_, sessionToken, _) = await SignInAsync();
        var (clientId, postLogoutUri) = await CreateClientAsync(disabled: true);

        using var response = await LogoutAsync(sessionToken, new()
        {
            ["client_id"] = clientId.ToString(),
            ["post_logout_redirect_uri"] = postLogoutUri
        });

        await AssertErrorAsync(response, "disabled");
    }

    [Fact]
    public async Task NotSignedIn_RedirectsRightAway()
    {
        var (clientId, postLogoutUri) = await CreateClientAsync();

        using var response = await LogoutAsync(sessionToken: null, new()
        {
            ["client_id"] = clientId.ToString(),
            ["post_logout_redirect_uri"] = postLogoutUri,
            ["state"] = State
        });

        AssertRedirectedTo(response, postLogoutUri);
    }

    [Fact]
    public async Task WithoutPostLogoutRedirectUri_ShowsTheResult()
    {
        var (userId, sessionToken, signedInAt) = await SignInAsync();
        var (clientId, _) = await CreateClientAsync();
        var idToken = await IssueIdTokenAsync(userId, clientId, signedInAt, DateTime.UtcNow);

        using var response = await LogoutAsync(sessionToken, new() { ["id_token_hint"] = idToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are signed out", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.True(await IsSessionRevokedAsync(userId));
    }

    [Fact]
    public async Task PostRequest_IsRedirectedToTheSameRequestAsGet()
    {
        var (clientId, postLogoutUri) = await CreateClientAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/logout")
        {
            Content = new FormUrlEncodedContent(
            [
                new("client_id", clientId.ToString()),
                new("post_logout_redirect_uri", postLogoutUri)
            ])
        };

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var query = QueryHelpers.ParseQuery(new Uri(AuthoraApiFactory.BaseAddress, response.Headers.Location!).Query);
        Assert.Equal(clientId.ToString(), query["client_id"].Single());
        Assert.Equal(postLogoutUri, query["post_logout_redirect_uri"].Single());
    }

    [Fact]
    public async Task PostLogoutRedirectUris_CanBeManagedThroughTheAdminApi()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");
        const string uri = "https://app.example.com/signed-out";

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Post, $"/api/clients/{clientId}/post-logout-redirect-uris", adminToken, new { uri }));
        Assert.Equal([uri], (await GetClientAsync(clientId, adminToken)).PostLogoutRedirectUris);

        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(HttpMethod.Post, $"/api/clients/{clientId}/post-logout-redirect-uris", adminToken, new { uri = "http://app.example.com/" }));

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}/post-logout-redirect-uris?uri={Uri.EscapeDataString(uri)}", adminToken));
        Assert.Empty((await GetClientAsync(clientId, adminToken)).PostLogoutRedirectUris);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var types = await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([AuditEventTypes.Clients.PostLogoutRedirectUriAdded, AuditEventTypes.Clients.PostLogoutRedirectUriRemoved], types);
    }

    [Fact]
    public async Task PostLogoutRedirectUris_WithoutUpdatePermission_IsForbidden()
    {
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(PermissionNames.Clients.Read), [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            await SendAsync(HttpMethod.Post, $"/api/clients/{clientId}/post-logout-redirect-uris", token, new { uri = "https://app.example.com/signed-out" }));
    }

    private async Task<(Guid UserId, string SessionToken, DateTime SignedInAt)> SignInAsync(DateTime? signedInAt = null)
    {
        // Whole seconds, like auth_time.
        var at = DateTimeOffset.FromUnixTimeSeconds(new DateTimeOffset(signedInAt ?? DateTime.UtcNow).ToUnixTimeSeconds()).UtcDateTime;
        var userId = await factory.CreateUserAsync();

        return (userId, await factory.CreateSessionAsync(userId, at), at);
    }

    private async Task<(Guid ClientId, string PostLogoutRedirectUri)> CreateClientAsync(bool disabled = false)
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var postLogoutRedirectUri = redirectUri.Replace("/callback", "/signed-out", StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);

        client.AddPostLogoutRedirectUri(postLogoutRedirectUri);

        if (disabled)
        {
            client.Disable();
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (clientId, postLogoutRedirectUri);
    }

    /// <summary>
    ///     Issues an ID Token like the token endpoint does, for a sign-in at <paramref name="authenticatedAt"/>.
    /// </summary>
    private async Task<string> IssueIdTokenAsync(Guid userId, Guid clientId, DateTime authenticatedAt, DateTime issuedAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var issuer = scope.ServiceProvider.GetRequiredService<IdTokenIssuer>();

        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        var client = await context.Clients.SingleAsync(client => client.Id == clientId, TestContext.Current.CancellationToken);
        var code = AuthorizationCode.Create(
            secretHasher.Hash("unused"), client, user, Guid.NewGuid(), null, ["openid"], null, null, authenticatedAt, issuedAt, TimeSpan.FromMinutes(1));

        return issuer.Issue(user, code, ["openid"], "unused-access-token", issuedAt);
    }

    private async Task<HttpResponseMessage> LogoutAsync(string? sessionToken, Dictionary<string, string> parameters)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            QueryHelpers.AddQueryString("/oauth/logout", parameters!));

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> ConfirmAsync(
        HttpResponseMessage confirmation,
        string sessionToken,
        Dictionary<string, string> parameters,
        string decision)
    {
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(confirmation);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/logout")
        {
            Content = new FormUrlEncodedContent(parameters
                .Append(new("decision", decision))
                .Append(new("__RequestVerificationToken", antiforgeryToken)))
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void AssertRedirectedTo(HttpResponseMessage response, string postLogoutRedirectUri)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal($"{postLogoutRedirectUri}?state={State}", response.Headers.Location!.OriginalString);
    }

    private static async Task AssertConfirmationAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign out of Authora?", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains(message, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    private async Task<bool> IsSessionRevokedAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Sessions.AnyAsync(session => session.UserId == userId && session.RevokedAt != null, TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> GetAuditTypesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == userId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);
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
