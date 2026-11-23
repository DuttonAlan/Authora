// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.ResetMfa;

/// <summary>
///     The Service for an administrator to reset the multi-factor authentication of a User, e.g. after they lost
///     their phone and their recovery codes: the authenticator app, the recovery codes and the passkeys are removed, so the password
///     alone signs the User in until they set up a new second factor.
/// </summary>
/// <remarks>
///     Rotates the Security Stamp, so all Sessions and Grants of the User end: whoever signed in with the lost factor
///     is signed out. The Sessions are revoked explicitly, so their Clients are notified (back-channel logout). The caller must be allowed to act on the User (<see cref="PrivilegeGuard"/>), so only an
///     administrator can reset an administrator.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to remove the authenticator and the codes.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to remove the passkeys.</param>
/// <param name="sessionRevoker">Revokes the Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the reset.</param>
/// <param name="logger">The Logger to record the reset.</param>
public sealed class ResetMfaService(
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    IPasskeyRepository passkeyRepository,
    SessionRevoker sessionRevoker,
    PrivilegeGuard privilegeGuard,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<ResetMfaService> logger)
{
    /// <summary>
    ///     Removes the authenticator app, the recovery codes and the passkeys of a User and signs the User out everywhere. Does
    ///     nothing if the User has none of them.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the reset is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted, or was changed by another request at the same time.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.MfaReset, user.Id);

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        // Joins the transaction of the guard, so a setup, new recovery codes or a passkey cannot be saved during the reset.
        await using var mfaTransaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        var authenticator = await mfaRepository.GetTotpAuthenticator(user.Id, cancellationToken);
        var codes = await mfaRepository.GetRecoveryCodes(user.Id, cancellationToken);
        var passkeys = await passkeyRepository.GetPasskeys(user.Id, cancellationToken);

        if (authenticator is null && codes.Count == 0 && passkeys.Count == 0)
        {
            return;
        }

        if (authenticator is not null)
        {
            mfaRepository.RemoveTotpAuthenticator(authenticator);
        }

        mfaRepository.RemoveRecoveryCodes(codes);
        passkeyRepository.RemovePasskeys(passkeys);
        user.RotateSecurityStamp();

        // Joins the transaction of the guard.
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, exceptSessionId: null, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        auditLog.Record(entry with { Details = DescribeRemoved(authenticator, codes.Count, passkeys.Count) });

        // User, authenticator and codes share the request's DbContext, so all are saved together with the audit event.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.MfaReset(user.Id);
    }

    private static string DescribeRemoved(TotpAuthenticator? authenticator, int recoveryCodes, int passkeys)
    {
        var parts = new List<string>();

        if (authenticator is not null)
        {
            parts.Add(authenticator.IsConfirmed ? "Authenticator app" : "Unfinished setup of an authenticator app");
        }

        parts.Add($"{recoveryCodes} recovery codes");
        parts.Add($"{passkeys} passkeys");

        return $"{string.Join(", ", parts)} removed";
    }
}
