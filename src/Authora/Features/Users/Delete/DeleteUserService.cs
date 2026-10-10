// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;

using Microsoft.EntityFrameworkCore;

namespace Authora.Features.Users.Delete;

/// <summary>
///     The Service to delete a User.
/// </summary>
/// <param name="context">The DbContext to access the Users.</param>
public sealed class DeleteUserService(AuthoraContext context)
{
    /// <summary>
    ///     Deletes a User based on the Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the User was deleted, false if the User does not exist.</returns>
    public async Task<bool> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deletedRows = await context.Users
            .Where(user => user.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedRows > 0;
    }
}
