// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users;

/// <summary>
///     Determines the Roles of a User together with where each Role comes from.
/// </summary>
internal static class EffectiveRoles
{
    /// <summary>
    ///     Gets the direct Roles and the Roles of the Groups of a User, each Role once, ordered by their Name.
    /// </summary>
    /// <remarks>
    ///     The Roles of the User, its Groups and their Roles must be loaded.
    /// </remarks>
    /// <param name="user">The User.</param>
    /// <returns>Each effective Role of the User together with its response, which names its sources.</returns>
    public static IReadOnlyList<(Role Role, EffectiveRoleResponse Response)> Resolve(User user)
    {
        var directRoleIds = user.Roles.Select(role => role.Id).ToHashSet();

        return user.GetEffectiveRoles()
            .Select(role => (Role: role, Response: new EffectiveRoleResponse(
                role.Id,
                role.Name,
                role.DisplayName,
                directRoleIds.Contains(role.Id),
                user.Groups
                    .Where(group => group.Roles.Any(groupRole => groupRole.Id == role.Id))
                    .OrderBy(group => group.Name, StringComparer.Ordinal)
                    .Select(group => new UserGroupResponse(group.Id, group.Name, group.DisplayName))
                    .ToList())))
            .OrderBy(x => x.Response.Name, StringComparer.Ordinal)
            .ToList();
    }
}
