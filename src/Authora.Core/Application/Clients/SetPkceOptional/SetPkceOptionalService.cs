// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients.SetPkceOptional;

/// <summary>
///     The Service to let a Confidential Client send authorization requests without PKCE, or to require PKCE again.
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to change the Client.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class SetPkceOptionalService(
    IClientRepository clientRepository,
    IAuditLog auditLog,
    ILogger<SetPkceOptionalService> logger)
{
    /// <summary>
    ///     Makes PKCE optional for a Confidential Client, or required again. Does nothing if it already is.
    /// </summary>
    /// <param name="id">Id of the Client.</param>
    /// <param name="isPkceOptional">True to accept authorization requests without PKCE, false to require it.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Client is changed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Client does not exist.</exception>
    /// <exception cref="DomainValidationException">Thrown when PKCE should become optional for a Public Client.</exception>
    public async Task ExecuteAsync(
        Guid id,
        bool isPkceOptional,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetClientById(id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");

        if (client.IsPkceOptional == isPkceOptional)
        {
            return;
        }

        client.SetPkceOptional(isPkceOptional);
        auditLog.Record(AuditEntry.ForClient(
            isPkceOptional ? AuditEventTypes.Clients.PkceMadeOptional : AuditEventTypes.Clients.PkceRequired,
            client.Id,
            $"Name '{client.Name}'"));

        await clientRepository.SaveChangesAsync(cancellationToken);

        if (isPkceOptional)
        {
            logger.ClientPkceMadeOptional(client.Id, client.Name);
        }
        else
        {
            logger.ClientPkceRequired(client.Id, client.Name);
        }
    }
}
