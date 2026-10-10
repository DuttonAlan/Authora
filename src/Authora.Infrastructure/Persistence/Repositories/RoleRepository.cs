// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Roles.</param>
public sealed class RoleRepository(AuthoraContext context) : IRoleRepository
{
    /// <inheritdoc/>
    public async Task<Role?> GetRoleById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Roles
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
    }
}
