// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Permissions.GetAll;
using Authora.Core.Application.Roles.Create;
using Authora.Core.Application.Roles.Delete;
using Authora.Core.Application.Roles.Get;
using Authora.Core.Application.Roles.GetAll;
using Authora.Core.Application.Roles.GrantPermission;
using Authora.Core.Application.Roles.RevokePermission;
using Authora.Core.Application.Roles.Update;
using Authora.Core.Application.Users.Activate;
using Authora.Core.Application.Users.AssignRole;
using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Deactivate;
using Authora.Core.Application.Users.Delete;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.GetPermissions;
using Authora.Core.Application.Users.GetRoles;
using Authora.Core.Application.Users.RemoveRole;
using Authora.Core.Application.Users.RevokeSessions;
using Authora.Core.Application.Users.Unlock;
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
        serviceCollection.AddTransient<GetAllPermissionsService>();

        serviceCollection.AddTransient<CreateRoleService>();
        serviceCollection.AddTransient<DeleteRoleService>();
        serviceCollection.AddTransient<GetAllRolesService>();
        serviceCollection.AddTransient<GetRoleService>();
        serviceCollection.AddTransient<GrantPermissionService>();
        serviceCollection.AddTransient<RevokePermissionService>();
        serviceCollection.AddTransient<UpdateRoleService>();

        serviceCollection.AddTransient<ActivateUserService>();
        serviceCollection.AddTransient<AssignRoleService>();
        serviceCollection.AddTransient<CreateUserService>();
        serviceCollection.AddTransient<DeactivateUserService>();
        serviceCollection.AddTransient<DeleteUserService>();
        serviceCollection.AddTransient<GetEffectivePermissionsService>();
        serviceCollection.AddTransient<GetUserService>();
        serviceCollection.AddTransient<GetUserRolesService>();
        serviceCollection.AddTransient<GetAllUsersService>();
        serviceCollection.AddTransient<RemoveRoleService>();
        serviceCollection.AddTransient<RevokeUserSessionsService>();
        serviceCollection.AddTransient<UnlockUserService>();
        serviceCollection.AddTransient<UpdateUserService>();

        return serviceCollection;
    }
}
