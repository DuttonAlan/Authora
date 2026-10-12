// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that only the token and revocation endpoints allow CORS, and only for the origins of enabled Clients
///     (TD-007).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class OAuthCorsTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/oauth/token")]
    [InlineData("/oauth/revoke")]
    public async Task PreflightFromOriginOfEnabledClient_IsAllowed(string endpoint)
    {
        var origin = await CreateClientAsync(ClientStatus.Enabled);

        using var response = await PreflightAsync(endpoint, origin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("POST", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task PostFromOriginOfEnabledClient_ReturnsCorsHeaderAlsoOnError()
    {
        var origin = await CreateClientAsync(ClientStatus.Enabled);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "authorization_code")])
        };
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        // The SPA must be able to read the OAuth error.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task PreflightFromOriginOfDisabledClient_IsNotAllowed()
    {
        var origin = await CreateClientAsync(ClientStatus.Disabled);

        using var response = await PreflightAsync("/oauth/token", origin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PreflightFromUnknownOrigin_IsNotAllowed()
    {
        using var response = await PreflightAsync("/oauth/token", "https://attacker.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/oauth/introspect")]
    [InlineData("/oauth/authorize")]
    [InlineData("/account/login")]
    [InlineData("/api/users")]
    public async Task PreflightToOtherEndpoint_IsNotAllowed(string endpoint)
    {
        var origin = await CreateClientAsync(ClientStatus.Enabled);

        using var response = await PreflightAsync(endpoint, origin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private async Task<string> CreateClientAsync(ClientStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        // A unique origin per test, so tests cannot pass because of another test's Client.
        var origin = $"https://{Guid.NewGuid():N}.example.com";

        var client = Client.Create("SPA", ClientType.Public);
        client.AddRedirectUri($"{origin}/callback");

        if (status == ClientStatus.Disabled)
        {
            client.Disable();
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return origin;
    }

    private async Task<HttpResponseMessage> PreflightAsync(string endpoint, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, endpoint);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
