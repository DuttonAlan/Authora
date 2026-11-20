// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that nobody grants more than they hold: a caller who is not an administrator can neither give out
///     Permissions they lack nor act on a User with more rights, and the attempt is recorded in the audit log.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class PrivilegeEscalationTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct horse battery staple";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AssignRoles_AdministratorRoleToSelf_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Users.AssignRoles);
        var administratorRoleId = await GetAdministratorRoleIdAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{callerId}/roles/{administratorRoleId}", token);

        await AssertForbiddenAsync(response);
        Assert.False(await HasRoleAsync(callerId, administratorRoleId));
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Users.RoleAssigned, callerId, callerId);
    }

    [Fact]
    public async Task AssignRoles_RoleWithPermissionsTheCallerLacks_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Users.AssignRoles);
        var roleId = await CreateRoleAsync(PermissionNames.Users.AssignRoles, PermissionNames.Users.Delete);

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{callerId}/roles/{roleId}", token);

        await AssertForbiddenAsync(response);
        Assert.False(await HasRoleAsync(callerId, roleId));
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Users.RoleAssigned, callerId, callerId);
    }

    [Fact]
    public async Task AssignRoles_RoleWithPermissionsTheCallerHolds_Succeeds()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Users.AssignRoles);
        var roleId = await CreateRoleAsync(PermissionNames.Users.AssignRoles);

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{callerId}/roles/{roleId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await HasRoleAsync(callerId, roleId));
    }

    [Fact]
    public async Task AssignRoles_ToUserWithMorePermissions_IsForbidden()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Users.AssignRoles);
        var targetId = await factory.CreateUserAsync(PermissionNames.Users.Delete);
        var roleId = await CreateRoleAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{targetId}/roles/{roleId}", token);

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task GrantPermissions_ToOwnRole_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Roles.GrantPermissions);
        var ownRoleId = await GetSingleRoleIdAsync(callerId);
        var permissionId = await GetPermissionIdAsync(PermissionNames.Users.AssignRoles);

        using var response = await SendAsync(HttpMethod.Put, $"/api/roles/{ownRoleId}/permissions/{permissionId}", token);

        await AssertForbiddenAsync(response);
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Roles.PermissionGranted, ownRoleId, callerId);
    }

    [Fact]
    public async Task GrantPermissions_PermissionTheCallerHolds_Succeeds()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Roles.GrantPermissions);
        var roleId = await CreateRoleAsync();
        var permissionId = await GetPermissionIdAsync(PermissionNames.Roles.GrantPermissions);

        using var response = await SendAsync(HttpMethod.Put, $"/api/roles/{roleId}/permissions/{permissionId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ManageMembers_JoinGroupWithMorePowerfulRole_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Groups.ManageMembers);
        var groupId = await CreateGroupAsync(await CreateRoleAsync(PermissionNames.Users.SetPassword));

        using var response = await SendAsync(HttpMethod.Put, $"/api/groups/{groupId}/members/{callerId}", token);

        await AssertForbiddenAsync(response);
        Assert.False(await IsMemberAsync(groupId, callerId));
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Groups.MemberAdded, groupId, callerId);
    }

    [Fact]
    public async Task ManageMembers_JoinGroupWithRolesTheCallerHolds_Succeeds()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Groups.ManageMembers);
        var groupId = await CreateGroupAsync(await CreateRoleAsync(PermissionNames.Groups.ManageMembers));

        using var response = await SendAsync(HttpMethod.Put, $"/api/groups/{groupId}/members/{callerId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await IsMemberAsync(groupId, callerId));
    }

    [Fact]
    public async Task GroupAssignRoles_MorePowerfulRole_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Groups.AssignRoles);
        var groupId = await CreateGroupAsync();
        var roleId = await CreateRoleAsync(PermissionNames.Users.SetPassword);

        using var response = await SendAsync(HttpMethod.Put, $"/api/groups/{groupId}/roles/{roleId}", token);

        await AssertForbiddenAsync(response);
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Groups.RoleAssigned, groupId, callerId);
    }

    [Fact]
    public async Task SetPassword_OfAdministrator_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Users.SetPassword);
        var administratorId = await factory.CreateAdministratorAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{administratorId}/password", token, new { Password });

        await AssertForbiddenAsync(response);
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Users.PasswordSet, administratorId, callerId);
    }

    [Fact]
    public async Task SetPassword_OfUserWithMorePermissions_IsForbidden()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Users.SetPassword);
        var targetId = await factory.CreateUserAsync(PermissionNames.Users.SetPassword, PermissionNames.Users.AssignRoles);

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{targetId}/password", token, new { Password });

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task SetPassword_OfUserWithoutRoles_Succeeds()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Users.SetPassword);
        var targetId = await factory.CreateUserAsync();
        await factory.RemoveRolesAsync(targetId);

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{targetId}/password", token, new { Password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SetPassword_OutsideOfARequest_IsForbidden()
    {
        // Without a caller, even a User without Roles, whose empty set of Permissions any caller holds, is protected.
        var targetId = await factory.CreateUserAsync();
        await factory.RemoveRolesAsync(targetId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<SetPasswordService>();

            await Assert.ThrowsAsync<ForbiddenException>(
                () => service.ExecuteAsync(targetId, new SetPasswordRequest(Password), TestContext.Current.CancellationToken));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var target = targetId.ToString();

            var auditEvent = await context.AuditEvents.SingleAsync(
                auditEvent => auditEvent.Type == AuditEventTypes.Users.PasswordSet && auditEvent.TargetId == target,
                TestContext.Current.CancellationToken);

            Assert.Equal(AuditOutcome.Failed, auditEvent.Outcome);
            Assert.Null(auditEvent.ActorUserId);
        }
    }

    [Fact]
    public async Task ManageAccess_DeactivateAdministrator_IsForbidden()
    {
        var (callerId, token) = await CreateCallerAsync(PermissionNames.Users.ManageAccess);
        var administratorId = await factory.CreateAdministratorAsync();

        using var response = await SendAsync(HttpMethod.Post, $"/api/users/{administratorId}/deactivate", token);

        await AssertForbiddenAsync(response);
        Assert.Equal(UserState.Active, await GetStateAsync(administratorId));
        await AssertDeniedAttemptRecordedAsync(AuditEventTypes.Users.Deactivated, administratorId, callerId);
    }

    [Theory]
    [InlineData("POST", "activate", PermissionNames.Users.ManageAccess)]
    [InlineData("POST", "unlock", PermissionNames.Users.ManageAccess)]
    [InlineData("POST", "revoke-sessions", PermissionNames.Users.ManageAccess)]
    [InlineData("DELETE", "", PermissionNames.Users.Delete)]
    public async Task ActOnAdministrator_IsForbidden(string method, string action, string permission)
    {
        var (_, token) = await CreateCallerAsync(permission);
        var administratorId = await factory.CreateAdministratorAsync();
        var uri = action.Length == 0 ? $"/api/users/{administratorId}" : $"/api/users/{administratorId}/{action}";

        using var response = await SendAsync(new HttpMethod(method), uri, token);

        await AssertForbiddenAsync(response);
        Assert.Equal(UserState.Active, await GetStateAsync(administratorId));
    }

    [Fact]
    public async Task UpdateAdministrator_IsForbidden()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Users.Update);
        var administratorId = await factory.CreateAdministratorAsync();

        using var response = await SendAsync(HttpMethod.Put, $"/api/users/{administratorId}", token, new { Email = $"{Guid.NewGuid():N}@example.com" });

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task RemoveRoleFromUserWithMorePermissions_IsForbidden()
    {
        var (_, token) = await CreateCallerAsync(PermissionNames.Users.AssignRoles);
        var targetId = await factory.CreateUserAsync(PermissionNames.Users.Delete);
        var roleId = await GetSingleRoleIdAsync(targetId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{targetId}/roles/{roleId}", token);

        await AssertForbiddenAsync(response);
        Assert.True(await HasRoleAsync(targetId, roleId));
    }

    [Fact]
    public async Task Administrator_CanStillDoEverything()
    {
        var administratorId = await factory.CreateAdministratorAsync();
        var token = await factory.IssueAccessTokenAsync(administratorId, [AdminApiScope.Name]);
        var otherAdministratorId = await factory.CreateAdministratorAsync();
        var userId = await factory.CreateUserAsync();
        var powerfulRoleId = await CreateRoleAsync(PermissionNames.Users.Delete);
        var groupId = await CreateGroupAsync(powerfulRoleId);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/users/{userId}/roles/{await GetAdministratorRoleIdAsync()}", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/roles/{powerfulRoleId}/permissions/{await GetPermissionIdAsync(PermissionNames.Users.AssignRoles)}", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{groupId}/members/{userId}", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/groups/{await CreateGroupAsync()}/roles/{powerfulRoleId}", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/users/{otherAdministratorId}/password", token, new { Password });
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/users/{otherAdministratorId}/deactivate", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Post, $"/api/users/{otherAdministratorId}/activate", token);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Delete, $"/api/users/{otherAdministratorId}", token);
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);

        Assert.Equal((int)HttpStatusCode.Forbidden, problem?.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
    }

    private async Task AssertDeniedAttemptRecordedAsync(string type, Guid targetId, Guid callerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var target = targetId.ToString();

        var auditEvent = await context.AuditEvents.SingleAsync(
            auditEvent => auditEvent.Type == type && auditEvent.TargetId == target,
            TestContext.Current.CancellationToken);

        Assert.Equal(AuditOutcome.Failed, auditEvent.Outcome);
        Assert.Equal(callerId, auditEvent.ActorUserId);
        Assert.Contains("Denied", auditEvent.Details, StringComparison.Ordinal);
    }

    private async Task<(Guid Id, string AccessToken)> CreateCallerAsync(string permission)
    {
        var userId = await factory.CreateUserAsync(permission);

        return (userId, await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]));
    }

    private async Task<Guid> CreateRoleAsync(params string[] permissionNames)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var role = Role.Create($"privilege-{Guid.NewGuid():N}", "Privilege Test");

        foreach (var permission in await context.Permissions
            .Where(permission => permissionNames.Contains(permission.Name))
            .ToListAsync(TestContext.Current.CancellationToken))
        {
            role.GrantPermission(permission);
        }

        context.Roles.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    private async Task<Guid> CreateGroupAsync(params Guid[] roleIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var group = Group.Create($"privilege-{Guid.NewGuid():N}", "Privilege Test");

        foreach (var role in await context.Roles.Where(role => roleIds.Contains(role.Id)).ToListAsync(TestContext.Current.CancellationToken))
        {
            group.AssignRole(role);
        }

        context.Groups.Add(group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return group.Id;
    }

    private async Task<Guid> GetAdministratorRoleIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Roles
            .Where(role => role.IsSystem && role.Name == Role.AdministratorName)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> GetPermissionIdAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Permissions
            .Where(permission => permission.Name == name)
            .Select(permission => permission.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> GetSingleRoleIdAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var user = await context.Users.Include(user => user.Roles).SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        return Assert.Single(user.Roles).Id;
    }

    private async Task<bool> HasRoleAsync(Guid userId, Guid roleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users.AnyAsync(
            user => user.Id == userId && user.Roles.Any(role => role.Id == roleId),
            TestContext.Current.CancellationToken);
    }

    private async Task<bool> IsMemberAsync(Guid groupId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users.AnyAsync(
            user => user.Id == userId && user.Groups.Any(group => group.Id == groupId),
            TestContext.Current.CancellationToken);
    }

    private async Task<UserState> GetStateAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.State)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task AssertStatusAsync(HttpStatusCode expected, HttpMethod method, string uri, string accessToken, object? body = null)
    {
        using var response = await SendAsync(method, uri, accessToken, body);

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
