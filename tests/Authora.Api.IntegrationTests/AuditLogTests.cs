// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Authora.Core.Application.Audit;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients.Register;
using Authora.Core.Application.Groups.Create;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that sign-ins and administrative changes are recorded with their actor, without secrets, and that only
///     authorized Users can search the audit log (US-034).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AuditLogTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Cookies are sent explicitly, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task AdminChange_IsRecordedWithActorAndTarget()
    {
        var adminId = await factory.CreateAdministratorAsync();
        var adminToken = await factory.IssueAccessTokenAsync(adminId, [AdminApiScope.Name]);

        var groupId = await CreateGroupAsync(adminToken);

        var auditEvent = Assert.Single(await SearchAsync(adminToken, $"type={AuditEventTypes.Groups.Created}&targetId={groupId}"));
        Assert.Equal(AuditOutcome.Succeeded, auditEvent.Outcome);
        Assert.Equal(adminId, auditEvent.ActorUserId);
        Assert.NotNull(auditEvent.ActorEmail);
        Assert.NotNull(auditEvent.ActorClientId);
        Assert.Equal(AuditTargetTypes.Group, auditEvent.TargetType);
        Assert.True(auditEvent.OccurredAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task RepeatedChange_IsRecordedOnce()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var userId = await factory.CreateUserAsync();
        var roleId = await GetRoleIdAsync(adminToken);

        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/users/{userId}/roles/{roleId}", adminToken);
        await AssertStatusAsync(HttpStatusCode.NoContent, HttpMethod.Put, $"/api/users/{userId}/roles/{roleId}", adminToken);

        Assert.Single(await SearchAsync(adminToken, $"type={AuditEventTypes.Users.RoleAssigned}&targetId={userId}"));
    }

    [Fact]
    public async Task RejectedChange_IsNotRecorded()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var openIdScopeId = await GetScopeIdAsync("openid");

        // A System Scope cannot be deleted.
        await AssertStatusAsync(HttpStatusCode.Conflict, HttpMethod.Delete, $"/api/scopes/{openIdScopeId}?force=true", adminToken);

        Assert.Empty(await SearchAsync(adminToken, $"type={AuditEventTypes.Scopes.Deleted}&targetId={openIdScopeId}"));
    }

    [Fact]
    public async Task ClientSecret_IsNeverRecorded()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var response = await SendAsync(HttpMethod.Post, "/api/clients", adminToken, new
        {
            Name = "Audited Client",
            Type = "Confidential",
            RedirectUris = new[] { "https://audited.example.com/callback" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var registered = (await response.Content.ReadFromJsonAsync<RegisterClientResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
        var events = await SearchAsync(adminToken, $"targetType=client&targetId={registered.Id}");

        Assert.Contains(events, auditEvent => auditEvent.Type == AuditEventTypes.Clients.Registered);
        Assert.Contains(events, auditEvent => auditEvent.Type == AuditEventTypes.Clients.SecretCreated);
        Assert.All(events, auditEvent => Assert.DoesNotContain(registered.Secret!.Secret, auditEvent.Details ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_RecordsFailureAndSuccess_WithoutPassword()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        using (var failed = await SignInAsync(email, "wrong-password-123"))
        {
            Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        }

        using (var succeeded = await SignInAsync(email, Password))
        {
            Assert.Equal(HttpStatusCode.Redirect, succeeded.StatusCode);
        }

        var events = await SearchAsync(adminToken, $"type={AuditEventTypes.Account.SignIn}&targetId={userId}");

        Assert.Equal(2, events.Count);
        Assert.Equal(AuditOutcome.Succeeded, events[0].Outcome);
        Assert.Equal(AuditOutcome.Failed, events[1].Outcome);
        Assert.Equal("Wrong password", events[1].Details);
        Assert.All(events, auditEvent => Assert.DoesNotContain("wrong-password", auditEvent.Details ?? string.Empty, StringComparison.Ordinal));
        Assert.All(events, auditEvent => Assert.Null(auditEvent.ActorUserId));

        var failures = await SearchAsync(adminToken, $"type=account&outcome=Failed&targetId={userId}");
        Assert.Equal(events[1].Id, Assert.Single(failures).Id);
    }

    [Fact]
    public async Task Search_ByArea_ReturnsOnlyEventsOfTheArea()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        await CreateGroupAsync(adminToken);

        var events = await SearchAsync(adminToken, "type=group");

        Assert.NotEmpty(events);
        Assert.All(events, auditEvent => Assert.StartsWith("group.", auditEvent.Type, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_WithoutPermission_IsForbidden()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Users.Read);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        await AssertStatusAsync(HttpStatusCode.Forbidden, HttpMethod.Get, "/api/audit-events", token);
    }

    [Fact]
    public async Task Search_WithPermission_IsAllowed()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Audit.Read);
        var token = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        await AssertStatusAsync(HttpStatusCode.OK, HttpMethod.Get, "/api/audit-events", token);
    }

    [Theory]
    [InlineData("from=2026-10-02T00:00:00Z&to=2026-10-01T00:00:00Z")]
    [InlineData("type=user%25")]
    [InlineData("pageSize=201")]
    public async Task Search_WithInvalidCriteria_IsRejected(string criteria)
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        await AssertStatusAsync(HttpStatusCode.BadRequest, HttpMethod.Get, $"/api/audit-events?{criteria}", adminToken);
    }

    [Fact]
    public async Task SigningKeys_AreRecordedAtStartup()
    {
        var adminToken = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
        var jwks = await _client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", TestContext.Current.CancellationToken);
        var signingKeyId = jwks.GetProperty("keys")[0].GetProperty("kid").GetString();

        // Recorded by a background service right after startup.
        IReadOnlyList<AuditEventResponse> events = [];

        for (var attempt = 0; attempt < 50 && events.Count == 0; attempt++)
        {
            events = await SearchAsync(adminToken, $"type={AuditEventTypes.System.SigningKeysChanged}");

            if (events.Count == 0)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        }

        var auditEvent = Assert.Single(events);
        Assert.Equal(signingKeyId, auditEvent.TargetId);
        Assert.Null(auditEvent.ActorUserId);
        Assert.Null(auditEvent.IpAddress);
    }

    [Fact]
    public async Task Cleanup_DeletesEventsOlderThanRetention()
    {
        var now = DateTime.UtcNow;

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var old = AuditEvent.Create("test.old", AuditOutcome.Succeeded, now.AddDays(-366), null, null, null, null, null, null);
        var recent = AuditEvent.Create("test.recent", AuditOutcome.Succeeded, now.AddDays(-364), null, null, null, null, null, null);
        context.AuditEvents.AddRange(old, recent);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>().CleanAsync(now, TestContext.Current.CancellationToken);

        var remaining = await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Id == old.Id || auditEvent.Id == recent.Id)
            .Select(auditEvent => auditEvent.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([recent.Id], remaining);
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        string antiforgeryToken;
        string antiforgeryCookie;

        using (var page = await _client.GetAsync("/account/login", TestContext.Current.CancellationToken))
        {
            (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/login")
        {
            Content = new FormUrlEncodedContent(
            [
                new("Email", email),
                new("Password", password),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CreateGroupAsync(string adminToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/groups", adminToken, new
        {
            Name = $"audit-{Guid.NewGuid():N}",
            DisplayName = "Audited Group"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CreateGroupResponse>(JsonOptions, TestContext.Current.CancellationToken))!.Id;
    }

    private async Task<Guid> GetRoleIdAsync(string adminToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/roles", adminToken, new
        {
            Name = $"audit-{Guid.NewGuid():N}",
            DisplayName = "Audited Role"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> GetScopeIdAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Scopes.Where(existing => existing.Name == name).Select(existing => existing.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<AuditEventResponse>> SearchAsync(string accessToken, string criteria)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/audit-events?{criteria}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<AuditEventResponse>>(JsonOptions, TestContext.Current.CancellationToken);

        return page!.Items;
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
