// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.Create;

/// <summary>
///     The Service to create a new Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to create a new Group.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateGroupService(
    IGroupRepository groupRepository,
    ILogger<CreateGroupService> logger)
{
    /// <summary>
    ///     Creates a new Group without Roles and members.
    /// </summary>
    /// <param name="request">The request to create the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Group.</returns>
    /// <exception cref="ConflictException">Thrown when the Name is already used by another Group.</exception>
    public async Task<Guid> ExecuteAsync(
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = Group.Create(request.Name, request.DisplayName, request.Description);

        if (await groupRepository.NameExists(group.Name, cancellationToken))
        {
            throw new ConflictException("The Name is already used by another Group.");
        }

        await groupRepository.CreateGroup(group, cancellationToken);

        logger.GroupCreated(group.Id, group.Name);

        return group.Id;
    }
}
