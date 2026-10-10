// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Data;
using Authora.Data.Entities;

namespace Authora.Features.Users.Create;

/// <summary>
///     The Service to create a new User.
/// </summary>
/// <param name="context"></param>
public sealed class CreateUserService(AuthoraContext context)
{
    /// <summary>
    ///     Creates a new User.
    /// </summary>
    /// <param name="request">Request to create a new User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created User.</returns>
    public async Task<Guid> ExecuteAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
