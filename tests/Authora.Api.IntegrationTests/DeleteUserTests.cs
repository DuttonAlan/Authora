// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Core.Application.Authorization;
using Authora.Core.Application.Users.Create;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that deleting a User erases the personal data and frees the Email (TD-005).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class DeleteUserTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Delete_AnonymizesUserAndFreesEmail()
    {
        var callerId = await factory.CreateUserAsync(PermissionNames.Users.Create, PermissionNames.Users.Delete);
        var token = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
        var email = $"{Guid.NewGuid():N}@example.com";

        var userId = await CreateAsync(email, token);

        using (var response = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            Assert.Equal(UserState.Deleted, user.State);
            Assert.Equal($"deleted-{userId:N}@{User.DeletedEmailDomain}", user.Email);
            Assert.Equal(user.Email, user.NormalizedEmail);
            Assert.Equal("Deleted", user.FirstName);
            Assert.Equal("User", user.LastName);
            Assert.Equal(User.DeletedPasswordHash, user.PasswordHash);
        }

        var newUserId = await CreateAsync(email, token);

        Assert.NotEqual(userId, newUserId);
    }

    private async Task<Guid> CreateAsync(string email, string accessToken)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/users",
            accessToken,
            new { Email = email, FirstName = "Jane", LastName = "Doe", Password = "correct horse battery staple" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(TestContext.Current.CancellationToken);

        return created!.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
