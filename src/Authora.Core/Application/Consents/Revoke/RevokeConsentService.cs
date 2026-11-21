// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Consents.Revoke;

/// <summary>
///     The Service for administrators to revoke the Consent of a User for a Client.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to check that the User exists.</param>
/// <param name="consentRepository">An Instance of a IConsentRepository to lock the Consent.</param>
/// <param name="consentRevoker">Revokes the Consent and the Grants of the User for the Client.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
public sealed class RevokeConsentService(
    IUserRepository userRepository,
    IConsentRepository consentRepository,
    ConsentRevoker consentRevoker,
    PrivilegeGuard privilegeGuard)
{
    /// <summary>
    ///     Revokes the Consent of a User for a Client and the Grants of the User for the Client, so the Client's Access
    ///     and Refresh Tokens for the User become invalid and the User is asked again. Does nothing if the User has
    ///     no Consent for the Client.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Consent is revoked.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(
        Guid userId,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        await using (var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken))
        {
            var user = await userRepository.GetUserById(userId, cancellationToken)
                ?? throw new NotFoundException($"User '{userId}' was not found.");

            await privilegeGuard.EnsureCanActOnUserAsync(
                user.Id,
                AuditEntry.ForUser(AuditEventTypes.Consents.Revoked, user.Id, $"Client {clientId}"),
                cancellationToken);

            // Joins the transaction of the guard and holds the lock of the Consent until it is committed.
            await using var consentTransaction = await consentRepository.BeginConsentChangeAsync(user.Id, clientId, cancellationToken);

            if (!await consentRevoker.RevokeAsync(user.Id, clientId, cancellationToken))
            {
                return;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await consentRevoker.RevokeRemainingGrantsAsync(userId, clientId, cancellationToken);
    }
}
