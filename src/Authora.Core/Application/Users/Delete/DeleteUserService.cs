// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.Delete;

/// <summary>
///     The Service to delete a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to delete a User.</param>
/// <param name="consentRepository">An Instance of a IConsentRepository to delete the Consents of the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to delete the authenticator app and recovery codes of the User.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to delete the passkeys of the User.</param>
/// <param name="sessionRevoker">Revokes the Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteUserService(
    IUserRepository userRepository,
    IConsentRepository consentRepository,
    IMfaRepository mfaRepository,
    IPasskeyRepository passkeyRepository,
    SessionRevoker sessionRevoker,
    PrivilegeGuard privilegeGuard,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<DeleteUserService> logger)
{
    /// <summary>
    ///     Deletes a User based on the Id.
    /// </summary>
    /// <remarks>
    ///     This is a soft delete: the User is set to <see cref="UserState.Deleted"/> and kept for history, with the
    ///     Email and Name anonymized, so the Email can be used by a new User.
    ///     The Security Stamp is rotated, so all Sessions and Refresh Tokens of the User become invalid. The Sessions
    ///     and their Grants are revoked, and their Clients notified (back-channel logout).
    ///     The Consents, the authenticator app, the recovery codes and the passkeys of the User are deleted.
    ///     Does nothing if the User does not exist or is already deleted.
    /// </remarks>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is deleted.</returns>
    /// <exception cref="ConflictException">Thrown when the User is the last administrator.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken);

        if (user is null || user.State == UserState.Deleted)
        {
            return;
        }

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.Deleted, user.Id);

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);
        await AdministratorGuard.EnsureNotLastAdministratorAsync(userRepository, user, cancellationToken);

        // In the transaction of the guard, so the Consents are only deleted together with the User.
        await consentRepository.DeleteConsentsOfUser(user.Id, cancellationToken);

        // The secret of the authenticator app, the recovery codes and the passkeys are of no use any more. The lock joins the
        // transaction of the guard, so no setup, new recovery codes or passkeys are saved meanwhile.
        await using var mfaTransaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        if (await mfaRepository.GetTotpAuthenticator(user.Id, cancellationToken) is { } authenticator)
        {
            mfaRepository.RemoveTotpAuthenticator(authenticator);
        }

        mfaRepository.RemoveRecoveryCodes(await mfaRepository.GetRecoveryCodes(user.Id, cancellationToken));
        passkeyRepository.RemovePasskeys(await passkeyRepository.GetPasskeys(user.Id, cancellationToken));

        user.MarkDeleted();

        // Joins the transaction of the guard.
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, exceptSessionId: null, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        auditLog.Record(entry);

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Again after the commit: an approval that was running in the meantime may have saved a Consent after the first
        // deletion. One saved after this point is deleted by the approval itself, because it checks the User after saving.
        await consentRepository.DeleteConsentsOfUser(user.Id, cancellationToken);

        logger.UserDeleted(user.Id);
    }
}
