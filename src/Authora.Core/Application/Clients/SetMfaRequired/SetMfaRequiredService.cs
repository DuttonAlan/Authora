// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.SetMfaRequired;

/// <summary>
///     The Service to require multi-factor authentication for the Users of a Client, or to no longer require it.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class SetMfaRequiredService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<SetMfaRequiredService> logger)
{
    /// <summary>
    ///     Requires multi-factor authentication for the Client, or no longer requires it. Does nothing if the Client
    ///     already has the setting.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="requiresMfa">True to require at least two factors, false to accept the password alone.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is changed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        bool requiresMfa,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.RequiresMfa == requiresMfa)
        {
            return;
        }

        client.SetRequiresMfa(requiresMfa);
        auditLog.Record(AuditEntry.ForClient(
            requiresMfa ? AuditEventTypes.Clients.MfaRequired : AuditEventTypes.Clients.MfaNoLongerRequired,
            client.Id,
            $"Name '{client.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        if (requiresMfa)
        {
            logger.ClientMfaRequired(client.Id, client.Name);
        }
        else
        {
            logger.ClientMfaNoLongerRequired(client.Id, client.Name);
        }
    }
}
