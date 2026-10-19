// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.SetFirstParty;

/// <summary>
///     The Service to mark a Client as first-party or as third-party.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class SetFirstPartyService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<SetFirstPartyService> logger)
{
    /// <summary>
    ///     Marks a Client as first-party, so Users are no longer asked for consent, or as third-party again.
    ///     Does nothing if the Client is already marked that way.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="isFirstParty">True to mark the Client as first-party, false to mark it as third-party.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is changed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        bool isFirstParty,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.IsFirstParty == isFirstParty)
        {
            return;
        }

        client.SetFirstParty(isFirstParty);
        auditLog.Record(AuditEntry.ForClient(
            isFirstParty ? AuditEventTypes.Clients.MarkedFirstParty : AuditEventTypes.Clients.MarkedThirdParty,
            client.Id,
            $"Name '{client.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        if (isFirstParty)
        {
            logger.ClientMarkedFirstParty(client.Id, client.Name);
        }
        else
        {
            logger.ClientMarkedThirdParty(client.Id, client.Name);
        }
    }
}
