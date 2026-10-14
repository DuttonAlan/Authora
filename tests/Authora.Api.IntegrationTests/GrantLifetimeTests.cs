// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Core.Application.OAuth;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a Grant has an absolute lifetime: rotating Refresh Tokens cannot keep it alive, and no token of it
///     expires later than the Grant itself.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class GrantLifetimeTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private static readonly WebApplicationFactoryClientOptions ClientOptions =
        new() { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false };

    [Fact]
    public async Task Tokens_DoNotOutliveTheGrant()
    {
        await using var shortGrants = factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:OAuth:GrantLifetime", "00:01:00"));
        using var http = shortGrants.CreateClient(ClientOptions);

        var (clientId, tokens) = await SignInAsync(http);

        // The Client's Access Token TTL is one hour, but the Grant ends after a minute.
        Assert.InRange(tokens.GetProperty("expires_in").GetInt32(), 0, 60);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var grant = await context.Grants.SingleAsync(grant => grant.ClientId == clientId, TestContext.Current.CancellationToken);
        var refreshToken = await context.RefreshTokens.SingleAsync(token => token.GrantId == grant.Id, TestContext.Current.CancellationToken);
        var accessToken = await context.AccessTokens.SingleAsync(token => token.GrantId == grant.Id, TestContext.Current.CancellationToken);

        Assert.Equal(grant.CreatedAt.AddMinutes(1), grant.ExpiresAt);
        Assert.True(refreshToken.ExpiresAt <= grant.ExpiresAt);
        Assert.True(accessToken.ExpiresAt <= grant.ExpiresAt);
    }

    [Fact]
    public async Task ExpiredGrant_RejectsRefreshTokenAndAccessToken()
    {
        using var http = factory.CreateClient(ClientOptions);

        var (clientId, tokens) = await SignInAsync(http);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var expiredAt = DateTime.UtcNow.AddSeconds(-1);

            await context.Grants
                .Where(grant => grant.ClientId == clientId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(grant => grant.ExpiresAt, expiredAt), TestContext.Current.CancellationToken);
        }

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", tokens.GetProperty("refresh_token").GetString())
        ]);

        using var refresh = await http.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
        var error = await refresh.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());

        using var userInfo = await http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    [InlineData("366.00:00:00")]
    public void GrantLifetime_OutOfRange_IsRejected(string lifetime)
    {
        var options = new OAuthOptions { Issuer = AuthoraApiFactory.Issuer, GrantLifetime = TimeSpan.Parse(lifetime, System.Globalization.CultureInfo.InvariantCulture) };

        var result = new OAuthOptionsValidator().Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(nameof(OAuthOptions.GrantLifetime), result.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Runs the Authorization Code flow for a new User and Public Client.
    /// </summary>
    private async Task<(Guid ClientId, JsonElement Tokens)> SignInAsync(HttpClient http)
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            http,
            AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"),
            sessionToken);

        return (clientId, await AuthorizationCodeFlow.ExchangeAsync(http, clientId, redirectUri, code));
    }
}
