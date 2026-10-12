// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Authora.Core.Application.Authorization;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the public health endpoints only return the overall status, and the details need a Permission
///     (TD-010).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class HealthEndpointTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task PublicEndpoint_ReturnsOnlyStatus(string endpoint)
    {
        using var response = await _client.GetAsync(endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var property = Assert.Single(body.RootElement.EnumerateObject());

        Assert.Equal("status", property.Name);
        Assert.Equal("Healthy", property.Value.GetString());
    }

    [Fact]
    public async Task Details_WithoutToken_IsUnauthorized()
    {
        using var response = await _client.GetAsync("/health/details", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Details_WithoutPermission_IsForbidden()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Read);

        using var response = await GetDetailsAsync(await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Details_WithPermission_ReturnsChecks()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Health.Read);

        using var response = await GetDetailsAsync(await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var checks = body.RootElement.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString());

        Assert.Contains("migrations", checks);
    }

    private async Task<HttpResponseMessage> GetDetailsAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/details");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
