// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth.Authorize;

namespace Authora.Core.Application.Consents.Check;

/// <summary>
///     The Service to decide whether the consent screen must be shown for an authorization request.
/// </summary>
/// <param name="consentRepository">An Instance of a IConsentRepository to load the Consent of the User.</param>
public sealed class CheckConsentService(IConsentRepository consentRepository)
{
    /// <summary>
    ///     Checks whether the User must be asked for consent, see <see cref="ValidatedAuthorizationRequest.RequiresConsent"/>.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the consent screen must be shown.</returns>
    public async Task<ConsentCheckResult> ExecuteAsync(
        ValidatedAuthorizationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A first-party Client never needs a stored Consent; only prompt=consent shows the screen.
        if (request.Client.IsFirstParty)
        {
            return ToResult(request.RequiresConsent(consent: null));
        }

        var consent = await consentRepository.GetConsent(userId, request.Client.Id, cancellationToken);

        return ToResult(request.RequiresConsent(consent));
    }

    private static ConsentCheckResult ToResult(bool requiresConsent)
    {
        return requiresConsent ? ConsentCheckResult.Required : ConsentCheckResult.NotRequired;
    }
}
