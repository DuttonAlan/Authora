// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Delete;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.Update;

using Microsoft.Extensions.DependencyInjection;

namespace Authora.Core;

/// <summary>
///     Defines Extension Methods for the IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registeres Application Logic to the Service Collection.
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <returns></returns>
    public static IServiceCollection AddApplication(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<CreateUserService>();
        serviceCollection.AddTransient<DeleteUserService>();
        serviceCollection.AddTransient<GetUserService>();
        serviceCollection.AddTransient<GetAllUsersService>();
        serviceCollection.AddTransient<UpdateUserService>();

        return serviceCollection;
    }
}
