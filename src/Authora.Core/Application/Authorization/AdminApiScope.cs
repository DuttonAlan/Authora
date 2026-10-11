// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Authorization;

/// <summary>
///     The System Scope an Access Token needs to call the admin API.
/// </summary>
/// <remarks>
///     The Scope only says that the User allowed the Client to manage Authora on their behalf. What the User may
///     actually do is still decided by their Permissions, see <see cref="PermissionEvaluator"/>.
/// </remarks>
public static class AdminApiScope
{
    /// <summary>
    ///     Name of the Scope.
    /// </summary>
    public const string Name = "authora.admin";

    /// <summary>
    ///     Display Name of the Scope, shown on the consent screen.
    /// </summary>
    public const string DisplayName = "Manage Authora";

    /// <summary>
    ///     Description of the Scope, shown on the consent screen.
    /// </summary>
    public const string Description = "Manage Users, Roles, Scopes and Clients of Authora with your Permissions.";
}
