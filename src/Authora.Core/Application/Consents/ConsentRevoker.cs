// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Consents;

/// <summary>
///     Revokes the Consent of a User for a Client together with the Grants of the User for the Client, for the User
///     on the account page and for administrators.
/// </summary>
/// <remarks>
///     A code exchange that runs at the same time may save a Grant after the Grants were revoked. It checks the
///     Consent after saving its Grant and revokes the Grant itself if the Consent is gone. Otherwise it saved the Grant
///     before the deletion of the Consent was committed, and <see cref="RevokeRemainingGrantsAsync"/> revokes it.
/// </remarks>
/// <param name="consentRepository">An Instance of a IConsentRepository to delete the Consent.</param>
/// <param name="tokenRepository">An Instance of a ITokenRepository to revoke the Grants.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the revocation.</param>
/// <param name="logger">The Logger to record the revocation.</param>
public sealed class ConsentRevoker(
    IConsentRepository consentRepository,
    ITokenRepository tokenRepository,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<ConsentRevoker> logger)
{
    /// <summary>
    ///     Deletes the Consent and revokes the Grants of the User for the Client. Must run in the transaction of
    ///     <see cref="IConsentRepository.BeginConsentChangeAsync"/>; call <see cref="RevokeRemainingGrantsAsync"/> once
    ///     the transaction is committed.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if there was a Consent to revoke.</returns>
    public async Task<bool> RevokeAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var consent = await consentRepository.GetConsent(userId, clientId, cancellationToken);

        if (consent is null)
        {
            return false;
        }

        var revokedGrants = await tokenRepository.RevokeGrantsOfUserForClient(
            userId,
            clientId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        consentRepository.RemoveConsent(consent);

        // The Client of the Consent is named in the Details; the Client of the event stays the one that acted, e.g. the
        // admin interface.
        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Consents.Revoked, userId, GetDetails(consent, revokedGrants)));

        await consentRepository.SaveChangesAsync(cancellationToken);

        logger.ConsentRevoked(userId, clientId, revokedGrants);

        return true;
    }

    /// <summary>
    ///     Revokes the Grants of the User for the Client again, after the deletion of the Consent was committed. A code
    ///     exchange that was running in the meantime may have created one.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Grants are revoked.</returns>
    public async Task RevokeRemainingGrantsAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await tokenRepository.RevokeGrantsOfUserForClient(userId, clientId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    private static string GetDetails(Consent consent, int revokedGrants)
    {
        var scopes = string.Join(' ', consent.Scopes.Select(scope => scope.Name).Order(StringComparer.Ordinal));

        return ConsentAuditDetails.Shorten($"Client {consent.ClientId}, {revokedGrants} Grant(s) revoked, Scopes '{scopes}'");
    }
}
