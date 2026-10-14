// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.RemoveMember;

/// <summary>
///     The Service to remove a User from a Group.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveGroupMemberService(
    IUserRepository userRepository,
    ILogger<RemoveGroupMemberService> logger)
{
    /// <summary>
    ///     Removes a User from a Group. The User loses the Roles of the Group unless they have them directly or through
    ///     another Group. Does nothing if the User is not a member.
    /// </summary>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is no longer a member.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    public async Task ExecuteAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var group = user.Groups.FirstOrDefault(group => group.Id == groupId);

        if (group is null)
        {
            return;
        }

        user.LeaveGroup(group.Id);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.MemberRemoved(group.Id, group.Name, user.Id);
    }
}
