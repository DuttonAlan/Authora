// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Infrastructure.HealthChecks;
using Authora.Infrastructure.Persistence;
using Authora.Infrastructure.Persistence.Interceptors;
using Authora.Infrastructure.Persistence.Repositories;
using Authora.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Authora.Infrastructure;

/// <summary>
///     Defines Extension Methods for the IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registeres the Infrastructure to the Service Collection.
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<ModifiedAtInterceptor>();

        var connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The connection string 'Database' is not configured. Set 'ConnectionStrings:Database', "
                + "e.g. via the environment variable 'ConnectionStrings__Database'.");
        }

        serviceCollection.AddDbContext<AuthoraContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<ModifiedAtInterceptor>());
        });

        serviceCollection.AddHealthChecks()
            .AddDbContextCheck<AuthoraContext>("database", tags: [HealthCheckTags.Ready])
            .AddCheck<MigrationsHealthCheck>("migrations", tags: [HealthCheckTags.Ready]);

        serviceCollection.AddSingleton<ISecretGenerator, SecretGenerator>();
        serviceCollection.AddSingleton<ISecretHasher, Sha256SecretHasher>();
        serviceCollection.AddSingleton<IPasswordHasher, PasswordHasher>();

        serviceCollection.AddTransient<IAuthorizationCodeRepository, AuthorizationCodeRepository>();
        serviceCollection.AddTransient<IClientRepository, ClientRepository>();
        serviceCollection.AddTransient<IPermissionRepository, PermissionRepository>();
        serviceCollection.AddTransient<IRoleRepository, RoleRepository>();
        serviceCollection.AddTransient<IScopeRepository, ScopeRepository>();
        serviceCollection.AddTransient<ISessionRepository, SessionRepository>();
        serviceCollection.AddTransient<ITokenRepository, TokenRepository>();
        serviceCollection.AddTransient<IUserRepository, UserRepository>();

        return serviceCollection;
    }
}
