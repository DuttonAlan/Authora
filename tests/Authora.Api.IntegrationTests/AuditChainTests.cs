// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Authora.Core.Application.Audit;
using Authora.Core.Application.Audit.Verify;
using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Auditing;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that the hash chain of the audit log detects events that were changed, deleted or inserted in the
///     database, and that a checkpoint detects a rebuilt or shortened chain (TD-013).
/// </summary>
/// <remarks>
///     The tests share one database and run one after another, so every test that breaks the chain repairs it again.
/// </remarks>
/// <param name="factory">The Api under test.</param>
public sealed class AuditChainTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task SealedChain_IsIntactAndMatchesItsCheckpoint()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var result = await VerifyAsync(token);

        Assert.True(result.IsIntact, result.Problem);
        Assert.NotNull(result.LastSequence);
        Assert.Equal(result.LastSequence - result.FirstSequence + 1, result.EventsChecked);
        Assert.Equal(0, result.UnsealedEvents);
        Assert.Null(result.CheckpointMatches);

        var checkpoint = await VerifyAsync(token, $"?checkpointSequence={result.LastSequence}&checkpointHash={result.LastHash}");

        Assert.True(checkpoint.IsIntact, checkpoint.Problem);
        Assert.True(checkpoint.CheckpointMatches);
    }

    [Fact]
    public async Task ChangedEvent_BreaksTheChain()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var (link, original) = await GetLastLinkAsync();

        await UpdateDetailsAsync(original.Id, "changed in the database");
        var result = await VerifyAsync(token);

        Assert.False(result.IsIntact);
        Assert.Equal(link.Sequence, result.BrokenAtSequence);
        Assert.Contains("changed", result.Problem, StringComparison.Ordinal);

        await UpdateDetailsAsync(original.Id, original.Details);
        Assert.True((await VerifyAsync(token)).IsIntact);
    }

    [Fact]
    public async Task DeletedEvent_BreaksTheChain()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var (link, original) = await GetLastLinkAsync();

        await ExecuteAsync(context => context.AuditEvents.Where(auditEvent => auditEvent.Id == original.Id).ExecuteDeleteAsync());
        var result = await VerifyAsync(token);

        Assert.False(result.IsIntact);
        Assert.Equal(link.Sequence, result.BrokenAtSequence);
        Assert.Contains("deleted", result.Problem, StringComparison.Ordinal);

        await ExecuteAsync(context =>
        {
            context.AuditEvents.Add(original);
            return context.SaveChangesAsync();
        });
        Assert.True((await VerifyAsync(token)).IsIntact);
    }

    [Fact]
    public async Task EventInsertedIntoThePast_IsReportedAsUnsealed()
    {
        var token = await CreateVerifierTokenAsync();
        await SealAsync();

        // Too old for the sealing to pick it up, like a forged event slipped in between old ones.
        var forged = AuditEvent.Create("user.created", AuditOutcome.Succeeded, DateTime.UtcNow.AddHours(-2), null, null, null, null, null, null);
        await ExecuteAsync(context =>
        {
            context.AuditEvents.Add(forged);
            return context.SaveChangesAsync();
        });
        await SealAsync();

        var result = await VerifyAsync(token);

        Assert.False(result.IsIntact);
        Assert.Equal(1, result.UnsealedEvents);
        Assert.Null(result.BrokenAtSequence);

        await ExecuteAsync(context => context.AuditEvents.Where(auditEvent => auditEvent.Id == forged.Id).ExecuteDeleteAsync());
        Assert.True((await VerifyAsync(token)).IsIntact);
    }

    [Fact]
    public async Task StartDeletedInTheDatabase_BreaksTheChain()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await CreateGroupAsync(token);
        await SealAsync();

        var before = await VerifyAsync(token);
        Assert.True(before.IsIntact, before.Problem);

        AuditChainLink first = null!;
        AuditEvent firstEvent = null!;

        // Deletes the first link and its event, as the cleanup would, but without recording it.
        await ExecuteAsync(async context =>
        {
            first = await context.AuditChainLinks.AsNoTracking().SingleAsync(link => link.Sequence == before.FirstSequence);
            firstEvent = await context.AuditEvents.AsNoTracking().SingleAsync(auditEvent => auditEvent.Id == first.EventId);

            await context.AuditEvents.Where(auditEvent => auditEvent.Id == first.EventId).ExecuteDeleteAsync();
            await context.AuditChainLinks.Where(link => link.Sequence == first.Sequence).ExecuteDeleteAsync();
        });

        var result = await VerifyAsync(token);

        Assert.False(result.IsIntact);
        Assert.Equal(first.Sequence, result.BrokenAtSequence);
        Assert.Contains("no cleanup recorded", result.Problem, StringComparison.Ordinal);

        await ExecuteAsync(context =>
        {
            context.AuditEvents.Add(firstEvent);
            context.AuditChainLinks.Add(first);
            return context.SaveChangesAsync();
        });
        Assert.True((await VerifyAsync(token)).IsIntact);
    }

    [Fact]
    public async Task ShortenedChain_DoesNotMatchTheCheckpoint()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var checkpoint = await VerifyAsync(token);
        var (link, original) = await GetLastLinkAsync();

        await ExecuteAsync(async context =>
        {
            await context.AuditChainLinks.Where(existing => existing.Sequence == link.Sequence).ExecuteDeleteAsync();
            await context.AuditEvents.Where(auditEvent => auditEvent.Id == original.Id).ExecuteDeleteAsync();
        });

        var result = await VerifyAsync(token, $"?checkpointSequence={checkpoint.LastSequence}&checkpointHash={checkpoint.LastHash}");

        Assert.False(result.IsIntact);
        Assert.False(result.CheckpointMatches);

        await ExecuteAsync(context =>
        {
            context.AuditEvents.Add(original);
            context.AuditChainLinks.Add(link);
            return context.SaveChangesAsync();
        });
        Assert.True((await VerifyAsync(token)).IsIntact);
    }

    [Fact]
    public async Task RebuiltChain_DoesNotMatchTheCheckpoint()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var checkpoint = await VerifyAsync(token);
        var (_, original) = await GetLastLinkAsync();

        // Someone with database access changes an event and lets the sealing rebuild the whole chain.
        await UpdateDetailsAsync(original.Id, "changed in the database");
        await ExecuteAsync(context => context.AuditChainLinks.ExecuteDeleteAsync());
        await SealAsync();

        var result = await VerifyAsync(token, $"?checkpointSequence={checkpoint.LastSequence}&checkpointHash={checkpoint.LastHash}");

        Assert.False(result.IsIntact);
        Assert.Null(result.BrokenAtSequence);
        Assert.False(result.CheckpointMatches);
    }

    [Fact]
    public async Task Cleanup_DeletesFromTheStartAndKeepsTheChainIntact()
    {
        var token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var (last, lastEvent) = await GetLastLinkAsync();

        // Far enough in the future that every event is beyond the retention.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>()
                .CleanAsync(DateTime.UtcNow.AddYears(2), TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            // The last link and its event are kept, so the chain continues.
            var remaining = Assert.Single(await context.AuditChainLinks.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(last.Sequence, remaining.Sequence);
            Assert.True(await context.AuditEvents.AnyAsync(auditEvent => auditEvent.Id == lastEvent.Id, TestContext.Current.CancellationToken));

            // The cut is recorded in the audit log itself, so the verification can tell it from tampering.
            Assert.True(await context.AuditEvents.AnyAsync(
                auditEvent => auditEvent.Type == AuditEventTypes.System.AuditChainTruncated
                    && auditEvent.TargetId == last.Sequence.ToString(CultureInfo.InvariantCulture),
                TestContext.Current.CancellationToken));
        }

        // The cleanup also deleted the token, which expired by then.
        token = await CreateVerifierTokenAsync();
        await CreateGroupAsync(token);
        await SealAsync();

        var result = await VerifyAsync(token);

        Assert.True(result.IsIntact, result.Problem);
        Assert.Equal(last.Sequence, result.FirstSequence);
    }

    [Theory]
    [InlineData(PermissionNames.Audit.Read)]
    [InlineData(PermissionNames.Users.Read)]
    public async Task Verify_WithoutVerifyPermission_IsForbidden(string permission)
    {
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(permission), [AdminApiScope.Name]);

        using var response = await SendAsync(HttpMethod.Get, "/api/audit-events/verification", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("?checkpointSequence=1")]
    [InlineData("?checkpointHash=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("?checkpointSequence=1&checkpointHash=not-a-hash")]
    public async Task Verify_WithIncompleteCheckpoint_IsBadRequest(string query)
    {
        var token = await CreateVerifierTokenAsync();

        using var response = await SendAsync(HttpMethod.Get, $"/api/audit-events/verification{query}", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> CreateVerifierTokenAsync()
    {
        var userId = await factory.CreateUserAsync(PermissionNames.Audit.Verify, PermissionNames.Groups.Create);

        return await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);
    }

    private async Task CreateGroupAsync(string token)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/groups", token, new
        {
            Name = $"chain-{Guid.NewGuid():N}",
            DisplayName = "Chained Group"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task SealAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<AuditChainSealer>().SealAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AuditChainVerificationResponse> VerifyAsync(string token, string query = "")
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/audit-events/verification{query}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<AuditChainVerificationResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<(AuditChainLink Link, AuditEvent Event)> GetLastLinkAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var link = await context.AuditChainLinks.AsNoTracking()
            .OrderByDescending(link => link.Sequence)
            .FirstAsync(TestContext.Current.CancellationToken);
        var auditEvent = await context.AuditEvents.AsNoTracking()
            .SingleAsync(auditEvent => auditEvent.Id == link.EventId, TestContext.Current.CancellationToken);

        return (link, auditEvent);
    }

    private Task UpdateDetailsAsync(Guid eventId, string? details)
    {
        return ExecuteAsync(context => context.AuditEvents
            .Where(auditEvent => auditEvent.Id == eventId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(auditEvent => auditEvent.Details, details)));
    }

    private async Task ExecuteAsync(Func<AuthoraContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        await action(scope.ServiceProvider.GetRequiredService<AuthoraContext>());
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
