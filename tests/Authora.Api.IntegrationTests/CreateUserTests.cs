// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Core.Abstractions;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Users.Create;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that a User is always created with a password, so the User can sign in right away (TD-001).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class CreateUserTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Endpoint = "/api/users";
    private const string Password = "correct horse battery staple";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateWithPassword_StoresHashOfPassword()
    {
        using var response = await PostAsync(new
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Password,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await context.Users.SingleAsync(user => user.Id == created!.Id, TestContext.Current.CancellationToken);

        Assert.True(user.CanSignIn);
        Assert.Equal(PasswordVerification.Success, passwordHasher.Verify(user.PasswordHash, Password));
    }

    [Fact]
    public async Task CreateWithoutPassword_IsBadRequest()
    {
        using var response = await PostAsync(new
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            FirstName = "Jane",
            LastName = "Doe",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithTooShortPassword_IsBadRequest()
    {
        using var response = await PostAsync(new
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Password = "too-short",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(object body)
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Create);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
