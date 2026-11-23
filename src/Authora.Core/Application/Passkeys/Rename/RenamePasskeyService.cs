// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys.Rename;

/// <summary>
///     The Service for a signed-in User to give one of their passkeys a new name.
/// </summary>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to change the passkey.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class RenamePasskeyService(
    IPasskeyRepository passkeyRepository,
    IAuditLog auditLog,
    ILogger<RenamePasskeyService> logger)
{
    /// <summary>
    ///     Renames a passkey of the User. Does nothing if the name stays the same.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="name">The new name, see <see cref="Passkey.NormalizeName"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the name is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the User has no passkey with this Id.</exception>
    /// <exception cref="DomainValidationException">Thrown when the name is invalid.</exception>
    public async Task ExecuteAsync(Guid userId, Guid passkeyId, string? name, CancellationToken cancellationToken = default)
    {
        var normalized = Passkey.NormalizeName(name);
        var passkey = await passkeyRepository.GetPasskey(userId, passkeyId, cancellationToken)
            ?? throw new NotFoundException($"Passkey '{passkeyId}' was not found.");

        if (string.Equals(passkey.Name, normalized, StringComparison.Ordinal))
        {
            return;
        }

        passkey.Rename(normalized);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Passkeys.Renamed, userId, $"Passkey {passkey.Id}"));

        await passkeyRepository.SaveChangesAsync(cancellationToken);

        logger.PasskeyRenamed(userId, passkey.Id);
    }
}
