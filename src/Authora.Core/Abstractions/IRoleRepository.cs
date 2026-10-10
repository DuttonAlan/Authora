// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Role operations.
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    ///     Get a Role by Id.
    /// </summary>
    /// <param name="id">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Role, or null if the Role does not exist.</returns>
    public Task<Role?> GetRoleById(Guid id, CancellationToken cancellationToken = default);
}
