// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.Enable;

/// <summary>
///     The Service to enable a Client.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to enable a Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class EnableClientService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<EnableClientService> logger)
{
    /// <summary>
    ///     Enables a Client, so it can obtain tokens again. Does nothing if the Client is already enabled.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is enabled.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.Status == ClientStatus.Enabled)
        {
            return;
        }

        client.Enable();
        auditLog.Record(AuditEntry.ForClient(AuditEventTypes.Clients.Enabled, client.Id, $"Name '{client.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        logger.ClientEnabled(client.Id, client.Name);
    }
}
