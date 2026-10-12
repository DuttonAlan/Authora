// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.Deactivate;

/// <summary>
///     The Service to deactivate a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to deactivate a User.</param>
/// <param name="logger">The Logger to record the deactivation.</param>
public sealed class DeactivateUserService(
    IUserRepository userRepository,
    ILogger<DeactivateUserService> logger)
{
    /// <summary>
    ///     Deactivates a User, so the User can no longer sign in.
    /// </summary>
    /// <remarks>
    ///     The Security Stamp is rotated, so all Sessions and Refresh Tokens of the User become invalid.
    ///     Does nothing if the User is already deactivated.
    /// </remarks>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is deactivated.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted or the last administrator.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await userRepository.BeginAdministratorChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        if (user.State == UserState.Deleted)
        {
            throw new ConflictException("A deleted User cannot be deactivated.");
        }

        if (user.State == UserState.Deactivated)
        {
            return;
        }

        await AdministratorGuard.EnsureNotLastAdministratorAsync(userRepository, user, cancellationToken);

        user.Deactivate();

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.UserDeactivated(user.Id);
    }
}
