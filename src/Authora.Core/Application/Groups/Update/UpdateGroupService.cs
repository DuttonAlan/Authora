// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.Update;

/// <summary>
///     The Service to update an existing Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to update a Group.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateGroupService(
    IGroupRepository groupRepository,
    ILogger<UpdateGroupService> logger)
{
    /// <summary>
    ///     Updates an existing Group.
    /// </summary>
    /// <param name="id">Id of the Group.</param>
    /// <param name="request">The request to update the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Group is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the new Name is already used by another Group.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetGroupById(id, cancellationToken)
            ?? throw new NotFoundException($"Group '{id}' was not found.");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();

            if (name != group.Name && await groupRepository.NameExists(name, cancellationToken))
            {
                throw new ConflictException("The Name is already used by another Group.");
            }

            group.SetName(name);
        }

        if (request.DisplayName is not null)
        {
            group.SetDisplayName(request.DisplayName);
        }

        if (request.Description is not null)
        {
            group.SetDescription(request.Description);
        }

        // Only changed values are written; ModifiedAt is set when saving.
        await groupRepository.SaveChangesAsync(cancellationToken);

        logger.GroupUpdated(group.Id, group.Name);
    }
}
