// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

using Authora.Core.Application.Authorization;
using Authora.Core.Application.Oidc;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a Client cannot serve a System Scope. Serving e.g. <c>openid</c> would make every sign-in of every
///     Client an active token for it at introspection.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ServeScopeTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(OidcScopes.OpenId)]
    [InlineData(OidcScopes.Profile)]
    [InlineData(OidcScopes.Email)]
    [InlineData(AdminApiScope.Name)]
    public async Task ServeSystemScope_IsConflict(string scopeName)
    {
        var clientId = await CreateConfidentialClientAsync();
        var scopeId = await GetScopeIdAsync(scopeName);

        using var response = await ServeAsync(clientId, scopeId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await IsServedAsync(clientId, scopeId));
    }

    [Fact]
    public async Task ServeRegularScope_ServesScope()
    {
        var clientId = await CreateConfidentialClientAsync();
        var scopeId = await CreateScopeAsync();

        using var response = await ServeAsync(clientId, scopeId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await IsServedAsync(clientId, scopeId));
    }

    private async Task<Guid> CreateConfidentialClientAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var client = Client.Create("Resource Server", ClientType.Confidential);
        context.Clients.Add(client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return client.Id;
    }

    private async Task<Guid> CreateScopeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var created = Scope.Create($"test.{Guid.NewGuid():N}", "Test");
        context.Scopes.Add(created);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return created.Id;
    }

    private async Task<Guid> GetScopeIdAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AuthoraContext>().Scopes
            .Where(scope => scope.Name == name)
            .Select(scope => scope.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> ServeAsync(Guid clientId, Guid scopeId)
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Clients.Update);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/clients/{clientId}/served-scopes/{scopeId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<bool> IsServedAsync(Guid clientId, Guid scopeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AuthoraContext>().Clients
            .Where(client => client.Id == clientId)
            .AnyAsync(client => client.ServedScopes.Any(served => served.Id == scopeId), TestContext.Current.CancellationToken);
    }
}
