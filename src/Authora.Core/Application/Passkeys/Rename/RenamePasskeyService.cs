// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys.Rename;

/// <summary>
///     The Service for a signed-in User to give one of their passkeys a new name.
/// </summary>
/// <remarks>
///     Like the other changes of passkeys, it needs a Session in which the User completed a second factor if they have
///     one, see <see cref="PasskeyChangeGuard"/>.
/// </remarks>
/// <param name="guard">Checks the Session.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to change the passkey.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class RenamePasskeyService(
    PasskeyChangeGuard guard,
    IPasskeyRepository passkeyRepository,
    IAuditLog auditLog,
    ILogger<RenamePasskeyService> logger)
{
    /// <summary>
    ///     Renames a passkey of the User. Does nothing if the name stays the same.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User renames it in.</param>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="name">The new name, see <see cref="Passkey.NormalizeName"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether it was renamed, or why not.</returns>
    /// <exception cref="DomainValidationException">Thrown when the name is invalid.</exception>
    public async Task<RenamePasskeyResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        Guid passkeyId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var normalized = Passkey.NormalizeName(name);

        var (user, _, needsStepUp) = await guard.CheckAsync(userId, sessionId, cancellationToken);

        if (user is null)
        {
            return RenamePasskeyResult.SessionEnded;
        }

        if (needsStepUp)
        {
            return RenamePasskeyResult.StepUpRequired;
        }

        var passkey = await passkeyRepository.GetPasskey(user.Id, passkeyId, cancellationToken);

        if (passkey is null)
        {
            return RenamePasskeyResult.NotFound;
        }

        if (string.Equals(passkey.Name, normalized, StringComparison.Ordinal))
        {
            return RenamePasskeyResult.Renamed;
        }

        passkey.Rename(normalized);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Passkeys.Renamed, user.Id, $"Passkey {passkey.Id}"));

        await passkeyRepository.SaveChangesAsync(cancellationToken);

        logger.PasskeyRenamed(user.Id, passkey.Id);

        return RenamePasskeyResult.Renamed;
    }
}

/// <summary>
///     The outcome of <see cref="RenamePasskeyService"/>.
/// </summary>
public enum RenamePasskeyResult
{
    /// <summary>
    ///     The passkey has the new name.
    /// </summary>
    Renamed,

    /// <summary>
    ///     The User has no passkey with this Id, e.g. because it was removed in another tab.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The User has a second factor but did not complete one in the Session, see <see cref="PasskeyChangeGuard"/>.
    /// </summary>
    StepUpRequired,

    /// <summary>
    ///     The Session ended in the meantime; the User must sign in again.
    /// </summary>
    SessionEnded
}
