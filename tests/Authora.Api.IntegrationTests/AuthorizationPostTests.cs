// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;

using Authora.Api.Authentication;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks authorization requests sent with POST (OpenID Connect Core 1.0, Section 3.1.2.1), and that the consent
///     form still needs its antiforgery token although the page accepts POST requests without one.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AuthorizationPostTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task PostRequest_IsRedirectedToTheSameRequestAsGet()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["ui_locales"] = "de en";

        using var response = await PostAsync(
            parameters.Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!)),
            sessionToken: null);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.Equal("/oauth/authorize", location.OriginalString.Split('?')[0]);

        var query = QueryHelpers.ParseQuery(new Uri(AuthoraApiFactory.BaseAddress, location).Query);
        Assert.Equal(parameters.Count, query.Count);
        Assert.All(parameters, parameter => Assert.Equal(parameter.Value, query[parameter.Key].Single()));
    }

    [Fact]
    public async Task PostRequest_KeepsRepeatedParameters_SoTheyAreRejected()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var form = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid")
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!))
            .Append(new("scope", "openid"))
            .ToList();

        using var post = await PostAsync(form, sessionToken: null);
        Assert.Equal(HttpStatusCode.SeeOther, post.StatusCode);

        using var get = await GetAsync(post.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.SeeOther, get.StatusCode);
        Assert.Equal("invalid_request", QueryHelpers.ParseQuery(get.Headers.Location!.Query)["error"].Single());
    }

    [Fact]
    public async Task PostRequest_WithoutForm_IsRedirectedAndRejectedAsGet()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/authorize");
        using var post = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.SeeOther, post.StatusCode);
        Assert.Equal("/oauth/authorize", post.Headers.Location!.OriginalString);

        using var get = await GetAsync(post.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, get.StatusCode);
    }

    [Fact]
    public async Task Consent_WithoutAntiforgeryToken_IsRejected()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var form = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid")
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!))
            .Append(new("decision", "allow"));

        using var response = await PostAsync(form, sessionToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Consent_WithAntiforgeryToken_StillIssuesCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        Assert.False(string.IsNullOrEmpty(code));
    }

    private async Task<HttpResponseMessage> PostAsync(IEnumerable<KeyValuePair<string, string>> form, string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/authorize") { Content = new FormUrlEncodedContent(form) };

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsync(string location)
    {
        return await _client.GetAsync(new Uri(location, UriKind.Relative), TestContext.Current.CancellationToken);
    }
}
