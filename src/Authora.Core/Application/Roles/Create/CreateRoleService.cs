// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles.Create;

/// <summary>
///     The Service to create a new Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to create a new Role.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateRoleService(
    IRoleRepository roleRepository,
    ILogger<CreateRoleService> logger)
{
    /// <summary>
    ///     Creates a new Role without Permissions.
    /// </summary>
    /// <param name="request">The request to create the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Role.</returns>
    /// <exception cref="ConflictException">Thrown when the Name is already used by another Role.</exception>
    public async Task<Guid> ExecuteAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = Role.Create(request.Name, request.DisplayName, request.Description);

        if (await roleRepository.NameExists(role.Name, cancellationToken))
        {
            throw new ConflictException("The Name is already used by another Role.");
        }

        await roleRepository.CreateRole(role, cancellationToken);

        logger.RoleCreated(role.Id, role.Name);

        return role.Id;
    }
}
