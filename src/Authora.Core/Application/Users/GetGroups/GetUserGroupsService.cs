// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Users.GetGroups;

/// <summary>
///     The Service to get the Groups of a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get the User.</param>
public sealed class GetUserGroupsService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets the Groups a User is a member of, ordered by their Name.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Groups of the User.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<UserGroupResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        return user.Groups
            .OrderBy(group => group.Name, StringComparer.Ordinal)
            .Select(group => new UserGroupResponse(group.Id, group.Name, group.DisplayName))
            .ToList();
    }
}
