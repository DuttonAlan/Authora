// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a Scope a Client serves is not deleted without force, so the resource server does not silently
///     lose it.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class DeleteScopeTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DeleteServedScopeWithoutForce_IsConflict()
    {
        var scopeId = await CreateServedScopeAsync();

        using var response = await DeleteAsync(scopeId, force: false);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await ScopeExistsAsync(scopeId));
    }

    [Fact]
    public async Task DeleteServedScopeWithForce_DeletesScope()
    {
        var scopeId = await CreateServedScopeAsync();

        using var response = await DeleteAsync(scopeId, force: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await ScopeExistsAsync(scopeId));
    }

    private async Task<Guid> CreateServedScopeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var served = Scope.Create($"test.{Guid.NewGuid():N}", "Test");
        var client = Client.Create("Resource Server", ClientType.Confidential);
        client.ServeScope(served);

        context.AddRange(served, client);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return served.Id;
    }

    private async Task<HttpResponseMessage> DeleteAsync(Guid scopeId, bool force)
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Scopes.Delete);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/scopes/{scopeId}?force={force}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<bool> ScopeExistsAsync(Guid scopeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AuthoraContext>().Scopes
            .AnyAsync(scope => scope.Id == scopeId, TestContext.Current.CancellationToken);
    }
}
