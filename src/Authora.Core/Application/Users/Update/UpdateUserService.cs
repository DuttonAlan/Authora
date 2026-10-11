// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Users.Update;

/// <summary>
///     The Service to update an existing User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to update a User.</param>
public sealed class UpdateUserService(IUserRepository userRepository)
{
    /// <summary>
    ///     Updates an existing User.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="request">The request to update the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted, or the new Email is already used by another User.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is empty, only whitespace, too long, or the Email is not valid.</exception>
    public async Task ExecuteAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        if (request.Email is not null)
        {
            var normalizedEmail = User.Normalize(request.Email);

            if (normalizedEmail != user.NormalizedEmail
                && await userRepository.EmailExists(normalizedEmail, cancellationToken))
            {
                throw new ConflictException("The Email is already used by another User.");
            }

            user.SetEmail(request.Email);
        }

        if (request.FirstName is not null)
        {
            user.SetFirstName(request.FirstName);
        }

        if (request.LastName is not null)
        {
            user.SetLastName(request.LastName);
        }

        // Only changed values are written; ModifiedAt is set when saving.
        await userRepository.SaveChangesAsync(cancellationToken);
    }
}
