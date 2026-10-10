// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Users.Delete;

/// <summary>
///     The Service to delete a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to delete a User.</param>
public sealed class DeleteUserService(IUserRepository userRepository)
{
    /// <summary>
    ///     Deletes a User based on the Id.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the User was deleted, false if the User does not exist.</returns>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await userRepository.DeleteUser(id, cancellationToken);
    }
}
