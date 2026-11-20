// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.OAuth.Authorize;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Consents.Remember;

/// <summary>
///     The Service to remember that a User approved an authorization request on the consent screen.
/// </summary>
/// <param name="consentRepository">An Instance of a IConsentRepository to store the Consent.</param>
/// <param name="timeProvider">The TimeProvider for the creation time.</param>
/// <param name="auditLog">The audit log to record the approval.</param>
/// <param name="logger">The Logger to record the approval.</param>
public sealed class RememberConsentService(
    IConsentRepository consentRepository,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<RememberConsentService> logger)
{
    /// <summary>
    ///     Creates the Consent of the User for the Client, or adds the requested Scopes it does not contain yet.
    ///     Does nothing if the Consent already contains all of them, e.g. after <c>prompt=consent</c>.
    /// </summary>
    /// <param name="request">The request the User approved.</param>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Consent is saved.</returns>
    public async Task ExecuteAsync(
        ValidatedAuthorizationRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = request.Client;

        await using var transaction = await consentRepository.BeginConsentChangeAsync(userId, client.Id, cancellationToken);

        var consent = await consentRepository.GetConsent(userId, client.Id, cancellationToken);
        IReadOnlyList<string> added;

        if (consent is null)
        {
            consent = Consent.Create(userId, client, request.Scopes, timeProvider.GetUtcNow().UtcDateTime);
            consentRepository.AddConsent(consent);
            added = request.Scopes.Select(scope => scope.Name).ToList();
        }
        else
        {
            added = consent.AddScopes(request.Scopes);
        }

        if (added.Count == 0)
        {
            return;
        }

        var scope = string.Join(' ', added);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Consents.Granted,
            userId,
            ConsentAuditDetails.Shorten($"Client {client.Id} '{client.Name}', added Scopes '{scope}'")) with
        {
            ClientId = client.Id
        });

        await consentRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.ConsentGranted(userId, client.Id, scope);
    }
}
