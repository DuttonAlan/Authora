// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;

namespace Authora.Infrastructure.Persistence.Repositories;

/// <inheritdoc/>
/// <param name="context">The DbContext to access the Permissions.</param>
public sealed class PermissionRepository(AuthoraContext context) : IPermissionRepository
{
    /// <inheritdoc/>
    public async Task<Permission?> GetPermissionById(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Permissions
            .FirstOrDefaultAsync(permission => permission.Id == id, cancellationToken);
    }
}
