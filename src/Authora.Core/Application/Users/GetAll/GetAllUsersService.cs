// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Users.GetAll;

/// <summary>
///     The Service to get all Users.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get all Users.</param>
public sealed class GetAllUsersService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets all Users ordered by their Email.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Users.</returns>
    public async Task<IReadOnlyList<GetAllUsersResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetUsers(cancellationToken);

        return users.Select(user => new GetAllUsersResponse(
               user.Id,
               user.Email,
               user.FirstName,
               user.LastName))
           .ToList();
    }
}
