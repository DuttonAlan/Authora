// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users.Create;

/// <summary>
///     The Service to create a new User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to create a new User.</param>
public sealed class CreateUserService(IUserRepository userRepository)
{
    /// <summary>
    ///     Creates a new User.
    /// </summary>
    /// <param name="request">The request to create the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created User.</returns>
    /// <exception cref="ConflictException">Thrown when the Email is already used by another User.</exception>
    public async Task<Guid> ExecuteAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = User.Create(request.Email, request.FirstName, request.LastName);

        if (await userRepository.EmailExists(user.NormalizedEmail, cancellationToken))
        {
            throw new ConflictException("The Email is already used by another User.");
        }

        await userRepository.CreateUser(user, cancellationToken);

        return user.Id;
    }
}
