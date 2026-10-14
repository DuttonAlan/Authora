// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Users.GetCurrent;

/// <summary>
///     The Service to get the signed-in User and their Permissions, e.g. for the admin interface to show only what
///     the User may do (US-033).
/// </summary>
/// <remarks>
///     Only for display: every endpoint still checks its Permission itself, see <see cref="PermissionEvaluator"/>.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User with their Permissions.</param>
public sealed class GetCurrentUserService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets the User and the Names of their effective Permissions.
    /// </summary>
    /// <param name="userId">Id of the User of the Access Token.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The User and their Permissions.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<GetCurrentUserResponse> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserWithPermissionsById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var roles = user.GetEffectiveRoles().ToList();

        // Same rule as PermissionEvaluator: the administrator Role has all Permissions, also those added since the
        // last seeding.
        var permissions = roles.Any(role => role.IsAdministrator)
            ? PermissionNames.All
            : roles.SelectMany(role => role.Permissions).Select(permission => permission.Name);

        return new GetCurrentUserResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            permissions.Distinct().Order(StringComparer.Ordinal).ToList());
    }
}
