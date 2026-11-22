// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Mfa.RegenerateRecoveryCodes;

/// <summary>
///     The Service for a signed-in User to replace their recovery codes with a new set, e.g. after using most of them
///     or when the old ones may have been seen. The previous codes, used or not, stop working.
/// </summary>
/// <param name="mfaRepository">An Instance of a IMfaRepository to replace the codes.</param>
/// <param name="secretHasher">The hasher of the codes.</param>
/// <param name="timeProvider">The TimeProvider for the creation time.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class RegenerateRecoveryCodesService(
    IMfaRepository mfaRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<RegenerateRecoveryCodesService> logger)
{
    /// <summary>
    ///     Replaces the recovery codes of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new codes in plain text, to show the User once.</returns>
    /// <exception cref="ConflictException">
    ///     Thrown when the User has no authenticator app, which the codes stand in for, or another request replaced the
    ///     codes at the same time.
    /// </exception>
    public async Task<IReadOnlyList<string>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await mfaRepository.HasConfirmedTotpAuthenticator(userId, cancellationToken))
        {
            throw new ConflictException("Recovery codes need an authenticator app. Set one up first.");
        }

        var codes = await RecoveryCodeIssuer.ReplaceAsync(
            mfaRepository,
            secretHasher,
            userId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Mfa.RecoveryCodesRegenerated, userId, $"{codes.Count} recovery codes"));

        await mfaRepository.SaveChangesAsync(cancellationToken);

        logger.RecoveryCodesRegenerated(userId);

        return codes;
    }
}
