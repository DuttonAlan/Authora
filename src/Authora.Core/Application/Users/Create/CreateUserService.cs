// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Application.Users.Create;

/// <summary>
///     The Service to create a new User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to create a new User.</param>
/// <param name="passwordHasher">The hasher for the initial password.</param>
public sealed class CreateUserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
{
    /// <summary>
    ///     Creates a new User with an initial password.
    /// </summary>
    /// <param name="request">The request to create the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created User.</returns>
    /// <exception cref="ConflictException">Thrown when the Email is already used by another User.</exception>
    /// <exception cref="DomainValidationException">Thrown when a value is invalid or the password does not follow the <see cref="PasswordPolicy"/>.</exception>
    public async Task<Guid> ExecuteAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        PasswordPolicy.ThrowIfInvalid(request.Password, nameof(request));

        // Checked before hashing, so a conflicting request does not pay for the hash.
        if (await userRepository.EmailExists(User.Normalize(request.Email), cancellationToken))
        {
            throw new ConflictException("The Email is already used by another User.");
        }

        var user = User.Create(request.Email, request.FirstName, request.LastName, passwordHasher.Hash(request.Password));

        await userRepository.CreateUser(user, cancellationToken);

        return user.Id;
    }
}
