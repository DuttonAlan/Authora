// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;

using Microsoft.EntityFrameworkCore;

namespace Authora.Features.Users.Update;

/// <summary>
///     The Service to update an existing User.
/// </summary>
/// <param name="context">The DbContext to access the Users.</param>
public sealed class UpdateUserService(AuthoraContext context)
{
    /// <summary>
    ///     Updates an existing User.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the User was updated, false if the User does not exist.</returns>
    public async Task<bool> ExecuteAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        var updated = false;

        if (request.Email is not null)
        {
            user.Email = request.Email.Trim();
            updated = true;
        }

        if (request.FirstName is not null)
        {
            user.FirstName = request.FirstName.Trim();
            updated = true;
        }

        if (request.LastName is not null)
        {
            user.LastName = request.LastName.Trim();
            updated = true;
        }

        if (updated)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
