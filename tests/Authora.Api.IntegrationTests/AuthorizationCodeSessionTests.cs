// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that an Authorization Code is bound to the Session in which it was issued.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AuthorizationCodeSessionTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Code_WhileSignedIn_GetsTokens()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Code_AfterSignOut_IsInvalidGrant()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        await SignOutAsync(sessionToken);

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", AuthorizationCodeFlow.CodeVerifier)
        ]);

        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
    }

    private async Task SignOutAsync(string sessionToken)
    {
        string antiforgeryToken;
        string antiforgeryCookie;

        using (var pageRequest = new HttpRequestMessage(HttpMethod.Get, "/account/logout"))
        {
            pageRequest.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");

            using var page = await _client.SendAsync(pageRequest, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);

            (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/logout")
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", antiforgeryToken)])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
