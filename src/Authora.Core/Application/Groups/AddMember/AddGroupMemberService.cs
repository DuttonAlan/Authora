// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.AddMember;

/// <summary>
///     The Service to add a User to a Group.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="groupRepository">An Instance of a IGroupRepository to get the Group.</param>
/// <param name="logger">The Logger to record the new membership.</param>
public sealed class AddGroupMemberService(
    IUserRepository userRepository,
    IGroupRepository groupRepository,
    ILogger<AddGroupMemberService> logger)
{
    /// <summary>
    ///     Adds a User to a Group, so the User has all Roles of the Group. Does nothing if the User is already a member.
    /// </summary>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is a member.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group or the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    public async Task ExecuteAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetGroupById(groupId, cancellationToken)
            ?? throw new NotFoundException($"Group '{groupId}' was not found.");

        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        if (user.Groups.Any(x => x.Id == group.Id))
        {
            return;
        }

        user.JoinGroup(group);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.MemberAdded(group.Id, group.Name, user.Id);
    }
}
