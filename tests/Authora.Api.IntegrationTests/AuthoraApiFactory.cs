// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Runs the Api against a PostgreSQL container, and creates the Users, tokens and Sessions the tests need.
/// </summary>
public sealed class AuthoraApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        // The Api does not apply migrations itself, and IdentitySeeder needs the tables at startup.
        var options = new DbContextOptionsBuilder<AuthoraContext>().UseNpgsql(_database.GetConnectionString()).Options;

        await using var context = new AuthoraContext(options);
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    ///     Creates a User who can sign in, with a Role that has the given Permissions.
    /// </summary>
    /// <param name="permissionNames">The Permissions of the Role of the User.</param>
    /// <returns>The Id of the User.</returns>
    public async Task<Guid> CreateUserAsync(params string[] permissionNames)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var role = Role.Create($"test-{Guid.NewGuid():N}", "Test");

        foreach (var permission in await context.Permissions.Where(permission => permissionNames.Contains(permission.Name)).ToListAsync())
        {
            role.GrantPermission(permission);
        }

        var user = CreateUser();
        user.AssignRole(role);

        context.AddRange(role, user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    ///     Creates a User who can sign in, with the administrator Role.
    /// </summary>
    /// <returns>The Id of the User.</returns>
    public async Task<Guid> CreateAdministratorAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var user = CreateUser();
        user.AssignRole(await context.Roles.SingleAsync(role => role.Name == Role.AdministratorName));

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    ///     Removes all Roles of a User.
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <returns>A Task that completes once the Roles are removed.</returns>
    public async Task RemoveRolesAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var user = await context.Users.Include(user => user.Roles).SingleAsync(user => user.Id == userId);

        foreach (var role in user.Roles.ToList())
        {
            user.RemoveRole(role.Id);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     Issues an Access Token to a User, like the Authorization Code flow does.
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <param name="scopes">The Scope Names of the token.</param>
    /// <param name="issuedAt">When the token was issued, now by default. The token is valid for one hour.</param>
    /// <returns>The plain-text Access Token.</returns>
    public async Task<string> IssueAccessTokenAsync(Guid userId, IReadOnlyList<string> scopes, DateTime? issuedAt = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretGenerator = scope.ServiceProvider.GetRequiredService<ISecretGenerator>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var now = issuedAt ?? DateTime.UtcNow;
        var user = await context.Users.SingleAsync(user => user.Id == userId);
        var client = Client.Create("Test Client", ClientType.Public);

        var authorizationCode = AuthorizationCode.Create(
            secretHasher.Hash(secretGenerator.Generate()), client, user, null, scopes, "test-challenge", now, TimeSpan.FromMinutes(1));
        var grant = Grant.Create(authorizationCode, now);

        var token = secretGenerator.Generate();
        var accessToken = AccessToken.Create(grant.Id, secretHasher.Hash(token), scopes, now, TimeSpan.FromHours(1));

        context.AddRange(client, authorizationCode, grant, accessToken);
        await context.SaveChangesAsync();

        return token;
    }

    /// <summary>
    ///     Creates a Session for a User, like signing in does.
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <returns>The plain-text Session token, the value of the Session cookie.</returns>
    public async Task<string> CreateSessionAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretGenerator = scope.ServiceProvider.GetRequiredService<ISecretGenerator>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var user = await context.Users.SingleAsync(user => user.Id == userId);

        var token = secretGenerator.Generate();
        context.Sessions.Add(Session.Create(user, secretHasher.Hash(token), DateTime.UtcNow, TimeSpan.FromHours(1)));
        await context.SaveChangesAsync();

        return token;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Not Development, so the bootstrap settings of appsettings.Development.json are not used.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
        builder.UseSetting("Authora:OAuth:Issuer", "https://localhost");
    }

    private static User CreateUser()
    {
        // The tests never sign in with the password.
        return User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", "not-a-password-hash");
    }
}
