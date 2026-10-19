// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Core.Application.Oidc;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the discovery document, the JWKS and the seeded OpenID Connect Scopes.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class OidcMetadataTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private static readonly string[] PublicJwkMembers = ["kty", "use", "alg", "kid", "n", "e"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Discovery_PublishesEndpointsBelowTheIssuer()
    {
        using var response = await _client.GetAsync("/.well-known/openid-configuration", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuthoraApiFactory.Issuer, document.GetProperty("issuer").GetString());
        Assert.Equal($"{AuthoraApiFactory.Issuer}/oauth/authorize", document.GetProperty("authorization_endpoint").GetString());
        Assert.Equal($"{AuthoraApiFactory.Issuer}/oauth/token", document.GetProperty("token_endpoint").GetString());
        Assert.Equal($"{AuthoraApiFactory.Issuer}/oauth/userinfo", document.GetProperty("userinfo_endpoint").GetString());
        Assert.Equal($"{AuthoraApiFactory.Issuer}/.well-known/jwks.json", document.GetProperty("jwks_uri").GetString());
        Assert.True(response.Headers.CacheControl?.Public);
    }

    [Fact]
    public async Task Discovery_DoesNotAdvertiseUnsupportedFeatures()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        Assert.Equal(["code"], Strings(document, "response_types_supported"));
        Assert.Equal(["query"], Strings(document, "response_modes_supported"));
        Assert.Equal(["RS256"], Strings(document, "id_token_signing_alg_values_supported"));
        Assert.Equal(["S256"], Strings(document, "code_challenge_methods_supported"));
        Assert.Equal(["public"], Strings(document, "subject_types_supported"));
        Assert.Equal(["openid", "profile", "email"], Strings(document, "scopes_supported"));

        // request_uri_parameter_supported defaults to true, so it must be sent as false.
        Assert.False(document.GetProperty("request_uri_parameter_supported").GetBoolean());
        Assert.False(document.GetProperty("request_parameter_supported").GetBoolean());
        Assert.False(document.GetProperty("claims_parameter_supported").GetBoolean());
    }

    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/jwks.json")]
    public async Task Metadata_AllowsCorsFromAnyOrigin(string endpoint)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Add("Origin", "https://any.example");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Jwks_PublishesSigningKeyAndPreviousKeyWithoutPrivateParts()
    {
        using var response = await _client.GetAsync("/.well-known/jwks.json", TestContext.Current.CancellationToken);
        var jwks = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var keys = jwks.GetProperty("keys").EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, keys.Count);
        Assert.Equal(2, keys.Select(key => key.GetProperty("kid").GetString()).Distinct().Count());

        foreach (var key in keys)
        {
            // Only public members: no d, p, q, dp, dq or qi.
            Assert.All(key.EnumerateObject(), member => Assert.Contains(member.Name, PublicJwkMembers));
            Assert.Equal("RSA", key.GetProperty("kty").GetString());
            Assert.Equal("sig", key.GetProperty("use").GetString());
            Assert.Equal("RS256", key.GetProperty("alg").GetString());
        }
    }

    [Fact]
    public async Task OidcScopes_AreSeededAsSystemScopes()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var names = OidcScopes.All.Select(oidcScope => oidcScope.Name).ToList();
        var seeded = await context.Scopes
            .Where(seededScope => names.Contains(seededScope.Name))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(names.Count, seeded.Count);
        Assert.All(seeded, seededScope => Assert.True(seededScope.IsSystem));
    }

    private static List<string?> Strings(JsonElement document, string name)
    {
        return document.GetProperty(name).EnumerateArray().Select(value => value.GetString()).ToList();
    }
}
