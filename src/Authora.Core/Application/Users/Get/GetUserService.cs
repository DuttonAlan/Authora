// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Users.Get;

/// <summary>
///     The Service to get a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get an User.</param>
public sealed class GetUserService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets a User based on the Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The User, or null if the User does not exist.</returns>
    public async Task<GetUserResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(id, cancellationToken);

        if (user is null)
        {
            throw new Exception();
        }

        return new GetUserResponse(user.Id, user.Email, user.FirstName, user.LastName);
    }
}
