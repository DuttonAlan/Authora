// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Authora.Core.Application.Authorization;
using Authora.Core.Application.Groups.Create;
using Authora.Core.Application.Groups.GetMembers;
using Authora.Core.Application.Paging;
using Authora.Core.Application.Users;
using Authora.Core.Application.Users.GetPermissions;
using Authora.Core.Domain.Entities;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that Users receive the Roles of their Groups and lose them again (US-029, US-030).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class GroupTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // A Permission the members only get through the Group, and an endpoint that checks it.
    private const string GroupPermission = PermissionNames.Users.Read;
    private const string ProtectedEndpoint = "/api/users";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Member_GetsPermissionOfGroupRole_UntilRemoved()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, memberToken) = await CreateUserWithoutPermissionsAsync();
        var roleId = await CreateRoleAsync(GroupPermission);
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Get, ProtectedEndpoint, memberToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.OK, HttpMethod.Get, ProtectedEndpoint, memberToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/groups/{groupId}/members/{memberId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Get, ProtectedEndpoint, memberToken);
    }

    [Fact]
    public async Task RoleRemovedFromGroup_RevokesPermissionOfMembers()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, memberToken) = await CreateUserWithoutPermissionsAsync();
        var roleId = await CreateRoleAsync(GroupPermission);
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.OK, HttpMethod.Get, ProtectedEndpoint, memberToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/groups/{groupId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Get, ProtectedEndpoint, memberToken);
    }

    [Fact]
    public async Task DeleteGroup_EndsMembershipsAndRevokesPermission()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, memberToken) = await CreateUserWithoutPermissionsAsync();
        var roleId = await CreateRoleAsync(GroupPermission);
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/groups/{groupId}", adminToken);

        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Get, ProtectedEndpoint, memberToken);

        var groups = await GetAsync<List<UserGroupResponse>>($"/api/users/{memberId}/groups", adminToken);
        Assert.Empty(groups);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        Assert.True(await context.Users.AnyAsync(user => user.Id == memberId, TestContext.Current.CancellationToken));
        Assert.True(await context.Roles.AnyAsync(role => role.Id == roleId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RolesAndPermissions_NameTheirSource()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, _) = await CreateUserWithoutPermissionsAsync();
        var roleId = await CreateRoleAsync(GroupPermission);
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);

        // The same Role directly and through the Group.
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/users/{memberId}/roles/{roleId}", adminToken);

        var roles = await GetAsync<List<EffectiveRoleResponse>>($"/api/users/{memberId}/roles", adminToken);
        var role = Assert.Single(roles, role => role.Id == roleId);
        Assert.True(role.IsDirect);
        Assert.Equal(groupId, Assert.Single(role.Groups).Id);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/users/{memberId}/roles/{roleId}", adminToken);

        var permissions = await GetAsync<List<GetEffectivePermissionsResponse>>($"/api/users/{memberId}/permissions", adminToken);
        var permission = Assert.Single(permissions, permission => permission.Name == GroupPermission);
        var grantedBy = Assert.Single(permission.GrantedBy);
        Assert.Equal(roleId, grantedBy.Id);
        Assert.False(grantedBy.IsDirect);
        Assert.Equal(groupId, Assert.Single(grantedBy.Groups).Id);
    }

    [Fact]
    public async Task AddMemberTwice_KeepsOneMembership()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, _) = await CreateUserWithoutPermissionsAsync();
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{memberId}", adminToken);

        var members = await GetAsync<PagedResponse<GetGroupMembersResponse>>($"/api/groups/{groupId}/members", adminToken);

        Assert.Equal(1, members.Total);
        Assert.Equal(memberId, Assert.Single(members.Items).Id);
    }

    [Fact]
    public async Task AddUnknownUserOrToUnknownGroup_IsNotFound()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var (memberId, _) = await CreateUserWithoutPermissionsAsync();
        var groupId = await CreateGroupAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NotFound, HttpMethod.Put, $"/api/groups/{groupId}/members/{Guid.NewGuid()}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NotFound, HttpMethod.Put, $"/api/groups/{Guid.NewGuid()}/members/{memberId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NotFound, HttpMethod.Put, $"/api/groups/{groupId}/roles/{Guid.NewGuid()}", adminToken);
    }

    [Fact]
    public async Task AssignAdministratorRoleToGroup_IsConflict()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var groupId = await CreateGroupAsync(adminToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var administratorRoleId = await scope.ServiceProvider.GetRequiredService<AuthoraContext>().Roles
            .Where(role => role.Name == Role.AdministratorName)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        await AssertStatusAsync(HttpStatusCode.Conflict, HttpMethod.Put, $"/api/groups/{groupId}/roles/{administratorRoleId}", adminToken);
    }

    [Fact]
    public async Task CreateGroupWithUsedName_IsConflict()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var name = $"group-{Guid.NewGuid():N}";

        await CreateGroupAsync(adminToken, name);

        using var response = await SendAsync(HttpMethod.Post, "/api/groups", adminToken, new { Name = name, DisplayName = "Again" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ManageMembersWithoutPermission_IsForbidden()
    {
        var adminToken = await IssueAdministratorTokenAsync();
        var groupId = await CreateGroupAsync(adminToken);

        var callerId = await factory.CreateUserAsync(PermissionNames.Groups.Read);
        var callerToken = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);

        await AssertStatusAsync(HttpStatusCode.OK, HttpMethod.Get, $"/api/groups/{groupId}", callerToken);
        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Put, $"/api/groups/{groupId}/members/{callerId}", callerToken);
    }

    private async Task<string> IssueAdministratorTokenAsync()
    {
        var administratorId = await factory.CreateAdministratorAsync();

        return await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
    }

    private async Task<(Guid Id, string AccessToken)> CreateUserWithoutPermissionsAsync()
    {
        var userId = await factory.CreateUserAsync();

        return (userId, await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]));
    }

    private async Task<Guid> CreateRoleAsync(string permissionName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var role = Role.Create($"group-test-{Guid.NewGuid():N}", "Group Test");
        role.GrantPermission(await context.Permissions.SingleAsync(
            permission => permission.Name == permissionName,
            TestContext.Current.CancellationToken));

        context.Roles.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    private async Task<Guid> CreateGroupAsync(string accessToken, string? name = null)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/groups",
            accessToken,
            new { Name = name ?? $"group-{Guid.NewGuid():N}", DisplayName = "Test Group" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateGroupResponse>(TestContext.Current.CancellationToken);

        return created!.Id;
    }

    private async Task<T> GetAsync<T>(string uri, string accessToken)
    {
        using var response = await SendAsync(HttpMethod.Get, uri, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    private async Task AssertStatusAsync(HttpStatusCode expected, HttpMethod method, string uri, string accessToken)
    {
        using var response = await SendAsync(method, uri, accessToken);

        Assert.Equal(expected, response.StatusCode);
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
