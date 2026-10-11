// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Permission operations.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    ///     Get a Permission by Id.
    /// </summary>
    /// <param name="id">Id of the Permission.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Permission, or null if the Permission does not exist.</returns>
    public Task<Permission?> GetPermissionById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Permissions ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Permissions.</returns>
    public Task<IReadOnlyList<Permission>> GetPermissions(CancellationToken cancellationToken = default);
}
