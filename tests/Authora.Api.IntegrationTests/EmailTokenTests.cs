// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Email;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Cleanup;
using Authora.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks the one-time tokens of email links: stored only as hashes, used once, only for their purpose, only
///     before they expire and only while the User has the address they were sent to.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class EmailTokenTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task Issue_StoresOnlyTheHash()
    {
        var userId = await factory.CreateUserAsync();
        var token = await IssueAsync(userId, EmailTokenPurpose.PasswordReset);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var stored = await context.EmailTokens.SingleAsync(emailToken => emailToken.UserId == userId, TestContext.Current.CancellationToken);

        Assert.NotEqual(token, stored.TokenHash.Value);
        Assert.Equal(secretHasher.Hash(token), stored.TokenHash);
        Assert.Equal(EmailTokenPurpose.PasswordReset, stored.Purpose);
        Assert.Null(stored.ConsumedAt);
    }

    [Fact]
    public async Task Redeem_ReturnsTheUserOnce()
    {
        var userId = await factory.CreateUserAsync();
        var token = await IssueAsync(userId, EmailTokenPurpose.PasswordReset);

        var first = await RedeemAsync(token, EmailTokenPurpose.PasswordReset);
        var second = await RedeemAsync(token, EmailTokenPurpose.PasswordReset);

        Assert.Equal(userId, first?.Id);
        Assert.Null(second);
    }

    [Fact]
    public async Task Redeem_ConcurrentRequests_OnlyOneSucceeds()
    {
        var userId = await factory.CreateUserAsync();
        var token = await IssueAsync(userId, EmailTokenPurpose.PasswordReset);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RedeemAsync(token, EmailTokenPurpose.PasswordReset)));

        Assert.Single(results, user => user is not null);
    }

    [Fact]
    public async Task Redeem_ForAnotherPurpose_IsRejected_AndKeepsTheToken()
    {
        var userId = await factory.CreateUserAsync();
        var token = await IssueAsync(userId, EmailTokenPurpose.EmailVerification);

        Assert.Null(await RedeemAsync(token, EmailTokenPurpose.PasswordReset));
        Assert.Equal(userId, (await RedeemAsync(token, EmailTokenPurpose.EmailVerification))?.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Redeem_UnknownToken_IsRejected(string token)
    {
        Assert.Null(await RedeemAsync(token, EmailTokenPurpose.PasswordReset));
        Assert.Null(await RedeemAsync(new string('a', EmailTokenRedeemer.MaxTokenLength + 1), EmailTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task Redeem_ExpiredToken_IsRejected()
    {
        var userId = await factory.CreateUserAsync();
        const string token = "expired-token";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            context.EmailTokens.Add(EmailToken.Create(
                secretHasher.Hash(token), user, EmailTokenPurpose.PasswordReset, DateTime.UtcNow.AddHours(-2), TimeSpan.FromHours(1)));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Null(await RedeemAsync(token, EmailTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task Redeem_AfterTheEmailChanged_IsRejected()
    {
        var userId = await factory.CreateUserAsync();
        var token = await IssueAsync(userId, EmailTokenPurpose.EmailVerification);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            user.SetEmail($"{Guid.NewGuid():N}@example.com");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Null(await RedeemAsync(token, EmailTokenPurpose.EmailVerification));
    }

    [Fact]
    public async Task Issue_ReplacesTheUnusedTokensOfThePurpose()
    {
        var userId = await factory.CreateUserAsync();
        var oldReset = await IssueAsync(userId, EmailTokenPurpose.PasswordReset);
        var verification = await IssueAsync(userId, EmailTokenPurpose.EmailVerification);
        var newReset = await IssueAsync(userId, EmailTokenPurpose.PasswordReset);

        Assert.Null(await RedeemAsync(oldReset, EmailTokenPurpose.PasswordReset));
        Assert.Equal(userId, (await RedeemAsync(newReset, EmailTokenPurpose.PasswordReset))?.Id);
        Assert.Equal(userId, (await RedeemAsync(verification, EmailTokenPurpose.EmailVerification))?.Id);
    }

    [Fact]
    public async Task Issue_Concurrently_LeavesOnlyOneUsableToken()
    {
        var userId = await factory.CreateUserAsync();

        var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => IssueAsync(userId, EmailTokenPurpose.PasswordReset)));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

            Assert.Equal(8, await context.EmailTokens.CountAsync(token => token.UserId == userId, TestContext.Current.CancellationToken));
            Assert.Equal(1, await context.EmailTokens.CountAsync(
                token => token.UserId == userId && token.ConsumedAt == null, TestContext.Current.CancellationToken));
        }

        var redeemed = new List<User?>();

        foreach (var token in tokens)
        {
            redeemed.Add(await RedeemAsync(token, EmailTokenPurpose.PasswordReset));
        }

        Assert.Single(redeemed, user => user is not null);
    }

    [Fact]
    public void Create_RejectsLifetimeAboveMaximum()
    {
        var user = User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", "hash");

        Assert.Throws<ArgumentOutOfRangeException>(() => EmailToken.Create(
            Core.Domain.ValueObjects.SecretHash.FromHashedValue("hash"),
            user,
            EmailTokenPurpose.PasswordReset,
            DateTime.UtcNow,
            EmailToken.MaxLifetime + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Cleanup_DeletesTokensExpiredBeforeRetention_AndKeepsTheRest()
    {
        var now = DateTime.UtcNow;
        var userId = await factory.CreateUserAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
            var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

            context.EmailTokens.AddRange(
                // Expired 30 days ago, beyond the default retention of 7 days.
                EmailToken.Create(secretHasher.Hash(Guid.NewGuid().ToString()), user, EmailTokenPurpose.PasswordReset, now.AddDays(-30), Lifetime),
                // Expired 2 days ago, within the retention.
                EmailToken.Create(secretHasher.Hash(Guid.NewGuid().ToString()), user, EmailTokenPurpose.PasswordReset, now.AddDays(-2), Lifetime),
                EmailToken.Create(secretHasher.Hash(Guid.NewGuid().ToString()), user, EmailTokenPurpose.EmailVerification, now, Lifetime));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ExpiredDataCleaner>().CleanAsync(now, TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
            var remaining = await context.EmailTokens.Where(token => token.UserId == userId).ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, remaining.Count);
            Assert.All(remaining, token => Assert.True(token.ExpiresAt > now.AddDays(-7)));
        }
    }

    private async Task<string> IssueAsync(Guid userId, EmailTokenPurpose purpose)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);

        return await scope.ServiceProvider.GetRequiredService<EmailTokenIssuer>()
            .IssueAsync(user, purpose, Lifetime, TestContext.Current.CancellationToken);
    }

    private async Task<User?> RedeemAsync(string token, EmailTokenPurpose purpose)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<EmailTokenRedeemer>()
            .RedeemAsync(token, purpose, TestContext.Current.CancellationToken);
    }
}
