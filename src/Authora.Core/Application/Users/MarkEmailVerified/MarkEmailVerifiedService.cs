// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.MarkEmailVerified;

/// <summary>
///     The Service for an administrator to mark the Email of a User as verified without a link, e.g. when the address
///     is known from another system.
/// </summary>
/// <remarks>
///     Applications may trust a verified address to belong to the User, e.g. to link accounts, so only mark addresses
///     the User is known to own. Open verification links of the User stop working.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load and save the User.</param>
/// <param name="emailTokenRepository">Revokes the open verification links.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class MarkEmailVerifiedService(
    IUserRepository userRepository,
    IEmailTokenRepository emailTokenRepository,
    PrivilegeGuard privilegeGuard,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<MarkEmailVerifiedService> logger)
{
    /// <summary>
    ///     Marks the Email of the User as verified. Does nothing if it is already verified.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.EmailMarkedVerified, user.Id);

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        if (user.IsEmailVerified)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        user.MarkEmailVerified(now);

        // A link is pointless now; revoked in the same transaction.
        await emailTokenRepository.RevokeEmailTokens(user.Id, EmailTokenPurpose.EmailVerification, now, cancellationToken);

        auditLog.Record(entry);

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.EmailMarkedVerified(user.Id);
    }
}
