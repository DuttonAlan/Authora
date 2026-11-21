// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Consents;

namespace Authora.Core.Application.Account.RevokeConsent;

/// <summary>
///     The Service for a signed-in User to revoke their own Consent for a Client on the account page.
/// </summary>
/// <param name="consentRepository">An Instance of a IConsentRepository to lock the Consent.</param>
/// <param name="consentRevoker">Revokes the Consent and the Grants of the User for the Client.</param>
public sealed class RevokeOwnConsentService(
    IConsentRepository consentRepository,
    ConsentRevoker consentRevoker)
{
    /// <summary>
    ///     Revokes the Consent of the User for a Client and the Grants of the User for the Client, so the Client's
    ///     Access and Refresh Tokens for the User become invalid and the User is asked again. Does nothing if the User
    ///     has no Consent for the Client.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="clientId">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if a Consent was revoked.</returns>
    public async Task<bool> ExecuteAsync(
        Guid userId,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        await using (var transaction = await consentRepository.BeginConsentChangeAsync(userId, clientId, cancellationToken))
        {
            if (!await consentRevoker.RevokeAsync(userId, clientId, cancellationToken))
            {
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        await consentRevoker.RevokeRemainingGrantsAsync(userId, clientId, cancellationToken);

        return true;
    }
}
