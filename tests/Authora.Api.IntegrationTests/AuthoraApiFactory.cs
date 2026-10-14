// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

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

    private readonly string _keyDirectory = Directory.CreateTempSubdirectory("authora-tests-").FullName;

    /// <summary>
    ///     The Issuer the Api is configured with.
    /// </summary>
    public const string Issuer = "https://localhost";

    /// <summary>
    ///     The address of the test clients. HTTPS, like in production: the antiforgery cookie is only issued over HTTPS.
    /// </summary>
    public static readonly Uri BaseAddress = new(Issuer);

    /// <summary>
    ///     Creates the factory. Clients created without options use <see cref="BaseAddress"/>.
    /// </summary>
    public AuthoraApiFactory()
    {
        ClientOptions.BaseAddress = BaseAddress;
    }

    /// <summary>
    ///     Path of the PEM file with the private key that signs ID Tokens.
    /// </summary>
    public string SigningKeyPath => Path.Combine(_keyDirectory, "signing-key.pem");

    /// <summary>
    ///     Path of the PEM file with the public key of a previous signing key, which is only published.
    /// </summary>
    public string PreviousKeyPath => Path.Combine(_keyDirectory, "previous-key.pem");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        using (var signingKey = RSA.Create(2048))
        using (var previousKey = RSA.Create(2048))
        {
            await File.WriteAllTextAsync(SigningKeyPath, signingKey.ExportPkcs8PrivateKeyPem());
            await File.WriteAllTextAsync(PreviousKeyPath, previousKey.ExportSubjectPublicKeyInfoPem());
        }

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

        Directory.Delete(_keyDirectory, recursive: true);
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
    ///     Creates a User who can sign in on the sign-in page with a password.
    /// </summary>
    /// <param name="password">The password of the User.</param>
    /// <returns>The Id and the Email of the User.</returns>
    public async Task<(Guid Id, string Email)> CreateUserWithPasswordAsync(string password)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = CreateUser(passwordHasher.Hash(password));

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return (user.Id, user.Email);
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
    /// <param name="clientId">The Client the token is issued to, or null for a new Public Client.</param>
    /// <returns>The plain-text Access Token.</returns>
    public async Task<string> IssueAccessTokenAsync(
        Guid userId,
        IReadOnlyList<string> scopes,
        DateTime? issuedAt = null,
        Guid? clientId = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretGenerator = scope.ServiceProvider.GetRequiredService<ISecretGenerator>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var now = issuedAt ?? DateTime.UtcNow;
        var user = await context.Users.SingleAsync(user => user.Id == userId);
        var client = clientId is null
            ? Client.Create("Test Client", ClientType.Public)
            : await context.Clients.SingleAsync(client => client.Id == clientId);

        var authorizationCode = AuthorizationCode.Create(
            secretHasher.Hash(secretGenerator.Generate()), client, user, null, scopes, "test-challenge", null, now, now, TimeSpan.FromMinutes(1));
        var grant = Grant.Create(authorizationCode, now);

        var token = secretGenerator.Generate();
        var accessToken = AccessToken.Create(grant.Id, secretHasher.Hash(token), scopes, now, TimeSpan.FromHours(1));

        context.AddRange(authorizationCode, grant, accessToken);

        if (clientId is null)
        {
            context.Add(client);
        }

        await context.SaveChangesAsync();

        return token;
    }

    /// <summary>
    ///     Creates a Session for a User, like signing in does.
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <param name="signedInAt">When the User signed in, now by default. The Session lasts one hour from then.</param>
    /// <returns>The plain-text Session token, the value of the Session cookie.</returns>
    public async Task<string> CreateSessionAsync(Guid userId, DateTime? signedInAt = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var secretGenerator = scope.ServiceProvider.GetRequiredService<ISecretGenerator>();
        var secretHasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();

        var user = await context.Users.SingleAsync(user => user.Id == userId);

        var token = secretGenerator.Generate();
        context.Sessions.Add(Session.Create(user, secretHasher.Hash(token), signedInAt ?? DateTime.UtcNow, TimeSpan.FromHours(1)));
        await context.SaveChangesAsync();

        return token;
    }

    /// <summary>
    ///     Creates an enabled Public Client with a Redirect URI that is allowed the given Scopes.
    /// </summary>
    /// <param name="scopeNames">The Names of existing Scopes, e.g. the seeded <c>openid</c>.</param>
    /// <returns>The Id and the Redirect URI of the Client.</returns>
    public async Task<(Guid ClientId, string RedirectUri)> CreatePublicClientAsync(params string[] scopeNames)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var redirectUri = $"https://{Guid.NewGuid():N}.example.com/callback";

        var client = Client.Create("Test Client", ClientType.Public);
        client.AddRedirectUri(redirectUri);

        foreach (var allowed in await context.Scopes.Where(allowed => scopeNames.Contains(allowed.Name)).ToListAsync())
        {
            client.AllowScope(allowed);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync();

        return (client.Id, redirectUri);
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Not Development, so the bootstrap settings of appsettings.Development.json are not used.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
        builder.UseSetting("Authora:OAuth:Issuer", Issuer);
        builder.UseSetting("Authora:Oidc:SigningKeyPath", SigningKeyPath);
        builder.UseSetting("Authora:Oidc:AdditionalKeyPaths:0", PreviousKeyPath);

        // Clients created by a test must be allowed for CORS right away.
        builder.UseSetting("Authora:OAuth:CorsOriginsCacheDuration", "00:00:00");
    }

    private static User CreateUser(string passwordHash = "not-a-password-hash")
    {
        // Only Users of CreateUserWithPasswordAsync sign in with their password.
        return User.Create($"{Guid.NewGuid():N}@example.com", "Test", "User", passwordHash);
    }
}
