// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.SetPassword;

/// <summary>
///     The Service for an administrator to set the password of a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="passwordHasher">The hasher for the new password.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class SetPasswordService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<SetPasswordService> logger)
{
    /// <summary>
    ///     Sets a new password. This rotates the Security Stamp, which ends all Sessions and invalidates all tokens
    ///     of the User.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="request">The request with the new password.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the password is set.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    /// <exception cref="DomainValidationException">Thrown when the password does not follow the <see cref="PasswordPolicy"/>.</exception>
    public async Task ExecuteAsync(
        Guid id,
        SetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Password is not { Length: >= PasswordPolicy.MinLength and <= PasswordPolicy.MaxLength })
        {
            throw new DomainValidationException(
                $"The password must be {PasswordPolicy.MinLength} to {PasswordPolicy.MaxLength} characters long.",
                nameof(request));
        }

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        user.SetPasswordHash(passwordHasher.Hash(request.Password));

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.PasswordSet(user.Id);
    }
}
