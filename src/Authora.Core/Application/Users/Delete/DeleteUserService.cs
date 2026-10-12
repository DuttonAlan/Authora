// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.Delete;

/// <summary>
///     The Service to delete a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to delete a User.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteUserService(
    IUserRepository userRepository,
    ILogger<DeleteUserService> logger)
{
    /// <summary>
    ///     Deletes a User based on the Id.
    /// </summary>
    /// <remarks>
    ///     This is a soft delete: the User is set to <see cref="UserState.Deleted"/> and kept for history, with the
    ///     Email and Name anonymized, so the Email can be used by a new User.
    ///     The Security Stamp is rotated, so all Sessions and Refresh Tokens of the User become invalid.
    ///     Does nothing if the User does not exist or is already deleted.
    /// </remarks>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is deleted.</returns>
    /// <exception cref="ConflictException">Thrown when the User is the last administrator.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await userRepository.BeginAdministratorChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken);

        if (user is null || user.State == UserState.Deleted)
        {
            return;
        }

        await AdministratorGuard.EnsureNotLastAdministratorAsync(userRepository, user, cancellationToken);

        user.MarkDeleted();

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.UserDeleted(user.Id);
    }
}
