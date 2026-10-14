// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles.Update;

/// <summary>
///     The Service to update an existing Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to update a Role.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateRoleService(
    IRoleRepository roleRepository,
    IAuditLog auditLog,
    ILogger<UpdateRoleService> logger)
{
    /// <summary>
    ///     Updates an existing Role.
    /// </summary>
    /// <param name="id">Id of the Role.</param>
    /// <param name="request">The request to update the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the Role does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the new Name is already used by another Role, or the Name of a System Role would change.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetRoleById(id, cancellationToken)
            ?? throw new NotFoundException($"Role '{id}' was not found.");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();

            if (name != role.Name)
            {
                if (role.IsSystem)
                {
                    throw new ConflictException("The Name of a System Role cannot be changed.");
                }

                if (await roleRepository.NameExists(name, cancellationToken))
                {
                    throw new ConflictException("The Name is already used by another Role.");
                }
            }

            role.SetName(name);
        }

        if (request.DisplayName is not null)
        {
            role.SetDisplayName(request.DisplayName);
        }

        if (request.Description is not null)
        {
            role.SetDescription(request.Description);
        }

        auditLog.Record(AuditEntry.ForRole(AuditEventTypes.Roles.Updated, role.Id, $"Name '{role.Name}'"));

        // Only changed values are written; ModifiedAt is set when saving.
        await roleRepository.SaveChangesAsync(cancellationToken);

        logger.RoleUpdated(role.Id, role.Name);
    }
}
