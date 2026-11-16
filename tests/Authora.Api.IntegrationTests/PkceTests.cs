// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients.Get;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that PKCE is required unless an administrator made it optional for a Confidential Client, and that a
///     Client for which it is optional cannot mix requests with and without PKCE.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class PkceTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Secret = "pkce-test-client-secret-0123456789";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Authorize_WithoutCodeChallenge_IsRejected()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: false);

        using var response = await AuthorizationCodeFlow.GetAsync(_client, WithoutPkce(clientId, redirectUri), sessionToken);

        AssertRedirectedError(response, "invalid_request");
    }

    [Fact]
    public async Task Authorize_PublicClient_WithoutCodeChallenge_IsRejected()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        using var response = await AuthorizationCodeFlow.GetAsync(_client, WithoutPkce(clientId, redirectUri), sessionToken);

        AssertRedirectedError(response, "invalid_request");
    }

    [Fact]
    public async Task PkceOptional_WithoutPkce_GetsTokens()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: true);
        var parameters = WithoutPkce(clientId, redirectUri);
        parameters["nonce"] = "test-nonce";

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        using var response = await ExchangeAsync(clientId, redirectUri, code, codeVerifier: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);
        Assert.Equal("test-nonce", claims.GetProperty("nonce").GetString());
    }

    [Fact]
    public async Task PkceOptional_WithPkce_StillChecksCodeVerifier()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: true);

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        using (var withoutVerifier = await ExchangeAsync(clientId, redirectUri, code, codeVerifier: null))
        {
            await AssertErrorAsync(withoutVerifier, "invalid_request");
        }

        code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        using var wrongVerifier = await ExchangeAsync(clientId, redirectUri, code, new string('x', 43));
        await AssertErrorAsync(wrongVerifier, "invalid_grant");
    }

    [Fact]
    public async Task PkceOptional_CodeWithoutChallenge_WithCodeVerifier_IsRejected()
    {
        // A code_verifier for a code issued without code_challenge is a sign of a PKCE downgrade attack.
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: true);

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, WithoutPkce(clientId, redirectUri), sessionToken);
        using var response = await ExchangeAsync(clientId, redirectUri, code, AuthorizationCodeFlow.CodeVerifier);

        await AssertErrorAsync(response, "invalid_grant");
    }

    [Fact]
    public async Task PkceOptional_MethodWithoutChallenge_IsRejected()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: true);
        var parameters = WithoutPkce(clientId, redirectUri);
        parameters["code_challenge_method"] = "S256";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedError(response, "invalid_request");
    }

    [Fact]
    public async Task CodeWithoutChallenge_AfterPkceIsRequiredAgain_IsRejected()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (clientId, redirectUri) = await CreateConfidentialClientAsync(isPkceOptional: true);

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, WithoutPkce(clientId, redirectUri), sessionToken);
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}/pkce-optional", adminToken));

        using var response = await ExchangeAsync(clientId, redirectUri, code, codeVerifier: null);

        await AssertErrorAsync(response, "invalid_grant");
    }

    [Fact]
    public async Task MakePkceOptional_IsRecordedAndCanBeUndone()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (clientId, _) = await CreateConfidentialClientAsync(isPkceOptional: false);

        Assert.False((await GetClientAsync(clientId, adminToken)).IsPkceOptional);

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/pkce-optional", adminToken));
        Assert.True((await GetClientAsync(clientId, adminToken)).IsPkceOptional);

        // Idempotent: no second event.
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/pkce-optional", adminToken));

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}/pkce-optional", adminToken));
        Assert.False((await GetClientAsync(clientId, adminToken)).IsPkceOptional);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var types = await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([AuditEventTypes.Clients.PkceMadeOptional, AuditEventTypes.Clients.PkceRequired], types);
    }

    [Fact]
    public async Task MakePkceOptional_PublicClient_IsBadRequest()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/pkce-optional", adminToken));
        Assert.False((await GetClientAsync(clientId, adminToken)).IsPkceOptional);
    }

    [Fact]
    public async Task MakePkceOptional_WithoutUpdatePermission_IsForbidden()
    {
        var token = await factory.IssueAccessTokenAsync(
            await factory.CreateUserAsync(PermissionNames.Clients.Read),
            [AdminApiScope.Name]);
        var (clientId, _) = await CreateConfidentialClientAsync(isPkceOptional: false);

        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/pkce-optional", token));
        Assert.False((await GetClientAsync(clientId, token)).IsPkceOptional);
    }

    [Fact]
    public async Task MakePkceOptional_UnknownClient_IsNotFound()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(HttpMethod.Put, $"/api/clients/{Guid.CreateVersion7()}/pkce-optional", adminToken));
    }

    private static Dictionary<string, string?> WithoutPkce(Guid clientId, string redirectUri)
    {
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters.Remove("code_challenge");
        parameters.Remove("code_challenge_method");

        return parameters;
    }

    private static void AssertRedirectedError(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(error, QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].Single());
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(error, body.GetProperty("error").GetString());
    }

    private async Task<HttpResponseMessage> ExchangeAsync(Guid clientId, string redirectUri, string code, string? codeVerifier)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", redirectUri)
        };

        if (codeVerifier is not null)
        {
            form.Add(new("code_verifier", codeVerifier));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token") { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{Secret}")));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(Guid ClientId, string RedirectUri)> CreateConfidentialClientAsync(bool isPkceOptional)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var redirectUri = $"https://{Guid.NewGuid():N}.example.com/callback";

        var client = Client.Create("PKCE Test Client", ClientType.Confidential);
        client.AddRedirectUri(redirectUri);
        client.AddSecret(secretHasher.Hash(Secret), DateTime.UtcNow, expiresAt: null);
        client.AllowScope(await context.Scopes.SingleAsync(allowed => allowed.Name == "openid", TestContext.Current.CancellationToken));
        client.SetPkceOptional(isPkceOptional);

        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (client.Id, redirectUri);
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
