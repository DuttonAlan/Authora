// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that expired rows are deleted after the retention, and only then (TD-008).
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class ExpiredDataCleanerTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    [Fact]
    public async Task Clean_DeletesRowsExpiredBeforeRetention_AndKeepsTheRest()
    {
        var now = DateTime.UtcNow;
        var userId = await factory.CreateUserAsync();

        // Expired 30 days ago, beyond the default retention of 7 days: code, Access Token and then the empty Grant go.
        await factory.IssueAccessTokenAsync(userId, ["openid"], now.AddDays(-30));

        // Still valid: everything stays.
        await factory.IssueAccessTokenAsync(userId, ["openid"], now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
            var currentGrant = await context.Grants
                .Where(grant => grant.UserId == userId)
                .OrderByDescending(grant => grant.CreatedAt)
                .FirstAsync(TestContext.Current.CancellationToken);

            context.AddRange(
                // Expired 2 days ago, within the retention: kept for reuse detection.
                RefreshToken.Create(currentGrant.Id, secretHasher.Hash(Guid.NewGuid().ToString()), now.AddDays(-3), TimeSpan.FromDays(1)),
                Session.Create(user, secretHasher.Hash(Guid.NewGuid().ToString()), now.AddDays(-30), TimeSpan.FromHours(1)),
                Session.Create(user, secretHasher.Hash(Guid.NewGuid().ToString()), now, TimeSpan.FromHours(1)));

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>().CleanAsync(now, TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var cancellationToken = TestContext.Current.CancellationToken;

            var grant = Assert.Single(await context.Grants.Where(grant => grant.UserId == userId).ToListAsync(cancellationToken));
            Assert.True(grant.CreatedAt > now.AddDays(-1));

            Assert.Single(await context.AccessTokens.Where(token => token.GrantId == grant.Id).ToListAsync(cancellationToken));
            Assert.Single(await context.RefreshTokens.Where(token => token.GrantId == grant.Id).ToListAsync(cancellationToken));
            Assert.Single(await context.AuthorizationCodes.Where(code => code.UserId == userId).ToListAsync(cancellationToken));
            Assert.Single(await context.Sessions.Where(session => session.UserId == userId).ToListAsync(cancellationToken));
        }
    }
}
