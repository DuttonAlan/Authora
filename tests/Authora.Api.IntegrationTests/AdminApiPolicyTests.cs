// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Reflection;

using Authora.Api.Authorization;
using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that every admin endpoint requires the admin API policy and a Permission, so a new or changed endpoint
///     cannot be left open by accident.
/// </summary>
public sealed class AdminApiPolicyTests
{
    public static TheoryData<string> AdminActions()
    {
        var data = new TheoryData<string>();

        foreach (var (controller, action) in GetAdminActions())
        {
            data.Add($"{controller.Name}.{action.Name}");
        }

        return data;
    }

    [Fact]
    public void AdminControllersExist()
    {
        Assert.NotEmpty(GetAdminActions());
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public void AdminAction_RequiresAdminApiPolicyAndPermission(string actionName)
    {
        var (controller, action) = GetAdminActions().Single(x => $"{x.Controller.Name}.{x.Action.Name}" == actionName);

        Assert.Contains(
            controller.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == AuthorizationPolicies.AdminApi);

        Assert.Contains(
            action.GetCustomAttributes<AuthorizeAttribute>(),
            attribute => PermissionNames.All.Contains(attribute.Policy));

        Assert.False(controller.IsDefined(typeof(AllowAnonymousAttribute)));
        Assert.False(action.IsDefined(typeof(AllowAnonymousAttribute)));
    }

    private static List<(Type Controller, MethodInfo Action)> GetAdminActions()
    {
        return typeof(AuthorizationPolicies).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.GetCustomAttribute<RouteAttribute>()?.Template.StartsWith("api/", StringComparison.Ordinal) == true)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.IsDefined(typeof(HttpMethodAttribute), inherit: true))
                .Select(action => (controller, action)))
            .ToList();
    }
}
