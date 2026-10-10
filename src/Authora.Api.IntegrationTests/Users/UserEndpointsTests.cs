// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Json;

using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.Update;

namespace Authora.Api.IntegrationTests.Users;

[Collection(ApiCollection.Name)]
public sealed class UserEndpointsTests(AuthoraApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ReturnsCreatedWithLocationOfNewUser()
    {
        var email = UniqueEmail();

        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest($" {email} ", " Jane ", " Doe "), CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(CancellationToken);
        Assert.NotNull(created);
        Assert.Equal($"/api/users/{created.Id}", response.Headers.Location?.OriginalString);

        var user = await _client.GetFromJsonAsync<GetUserResponse>(response.Headers.Location, CancellationToken);
        Assert.Equal(new GetUserResponse(created.Id, email, "Jane", "Doe"), user);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}", CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ContainsCreatedUser()
    {
        var id = await CreateUser(UniqueEmail());

        var users = await _client.GetFromJsonAsync<List<GetAllUsersResponse>>("/api/users", CancellationToken);

        Assert.NotNull(users);
        Assert.Contains(users, user => user.Id == id);
    }

    [Fact]
    public async Task Update_ChangesOnlyGivenFields()
    {
        var email = UniqueEmail();
        var id = await CreateUser(email);

        var response = await _client.PutAsJsonAsync($"/api/users/{id}", new UpdateUserRequest(null, "Janet", null), CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var user = await _client.GetFromJsonAsync<GetUserResponse>($"/api/users/{id}", CancellationToken);
        Assert.Equal(new GetUserResponse(id, email, "Janet", "Doe"), user);
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        var id = await CreateUser(UniqueEmail());

        var response = await _client.DeleteAsync($"/api/users/{id}", CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await _client.GetAsync($"/api/users/{id}", CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private static string UniqueEmail()
    {
        return $"{Guid.NewGuid():N}@example.com";
    }

    private async Task<Guid> CreateUser(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "Jane", "Doe"), CancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(CancellationToken);
        return created!.Id;
    }
}
