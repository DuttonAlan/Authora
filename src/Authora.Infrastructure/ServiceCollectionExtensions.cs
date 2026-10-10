// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Infrastructure.Persistence;
using Authora.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        serviceCollection.AddDbContext<AuthoraContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Database"));
        });

        serviceCollection.AddTransient<IUserRepository, UserRepository>();

        return serviceCollection;
    }
}
