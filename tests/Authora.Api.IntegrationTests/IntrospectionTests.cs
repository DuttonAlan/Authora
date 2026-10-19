// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a Confidential Client can only introspect Access Tokens issued to itself or containing a Scope it
///     serves, and only sees the served Scopes.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class IntrospectionTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Secret = "resource-server-secret";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ResourceServer_SeesOnlyServedScopes()
    {
        var served = await CreateScopeAsync();
        var other = await CreateScopeAsync();
        var resourceServerId = await CreateConfidentialClientAsync(served);
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), [served.Name, other.Name]);

        var response = await IntrospectAsync(resourceServerId, token);

        Assert.True(response.Active);
        Assert.Equal(served.Name, response.Scope);
    }

    [Fact]
    public async Task ClientWithoutServedScope_SeesTokenAsInactive()
    {
        var scope = await CreateScopeAsync();
        var clientId = await CreateConfidentialClientAsync();
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), [scope.Name]);

        var response = await IntrospectAsync(clientId, token);

        Assert.False(response.Active);
        Assert.Null(response.Scope);
        Assert.Null(response.Subject);
    }

    [Fact]
    public async Task Client_SeesAllScopesOfOwnToken()
    {
        var first = await CreateScopeAsync();
        var second = await CreateScopeAsync();
        var clientId = await CreateConfidentialClientAsync();
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), [first.Name, second.Name], clientId: clientId);

        var response = await IntrospectAsync(clientId, token);

        Assert.True(response.Active);
        Assert.Equal($"{first.Name} {second.Name}", response.Scope);
    }

    private async Task<Scope> CreateScopeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var created = Scope.Create($"test.{Guid.NewGuid():N}", "Test");
        context.Scopes.Add(created);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return created;
    }

    private async Task<Guid> CreateConfidentialClientAsync(params Scope[] servedScopes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var client = Client.Create("Resource Server", ClientType.Confidential);
        client.AddSecret(secretHasher.Hash(Secret), DateTime.UtcNow, expiresAt: null);

        foreach (var served in servedScopes)
        {
            context.Attach(served);
            client.ServeScope(served);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client.Id;
    }

    private async Task<Introspection> IntrospectAsync(Guid clientId, string token)
    {
        using var content = new FormUrlEncodedContent(
        [
            new("client_id", clientId.ToString()),
            new("client_secret", Secret),
            new("token", token),
        ]);

        using var response = await _client.PostAsync("/oauth/introspect", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<Introspection>(TestContext.Current.CancellationToken))!;
    }

    private sealed record Introspection(
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("sub")] string? Subject);
}
