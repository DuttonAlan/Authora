// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;

using Microsoft.EntityFrameworkCore;

namespace Authora.Features.Users.Get;

/// <summary>
///     Gets a User.
/// </summary>
/// <param name="context"></param>
public sealed class GetUserService(AuthoraContext context)
{
    /// <summary>
    ///     Gets a User based on the Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns></returns>
    public async Task<GetUserResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
           .AsNoTracking()
           .Where(user => user.Id == id)
           .Select(user => new GetUserResponse(
               user.Id,
               user.Email,
               user.FirstName,
               user.LastName))
           .SingleOrDefaultAsync(cancellationToken);
    }
}
