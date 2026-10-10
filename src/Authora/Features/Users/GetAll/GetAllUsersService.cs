// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;

using Microsoft.EntityFrameworkCore;

namespace Authora.Features.Users.GetAll;

/// <summary>
///     The Service to get all Users.
/// </summary>
/// <param name="context">The DbContext to access the Users.</param>
public sealed class GetAllUsersService(AuthoraContext context)
{
    /// <summary>
    ///     Gets all Users ordered by their Email.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Users.</returns>
    public async Task<IReadOnlyList<GetAllUsersResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Users
           .AsNoTracking()
           .OrderBy(user => user.Email)
           .Select(user => new GetAllUsersResponse(
               user.Id,
               user.Email,
               user.FirstName,
               user.LastName))
           .ToListAsync(cancellationToken);
    }
}
