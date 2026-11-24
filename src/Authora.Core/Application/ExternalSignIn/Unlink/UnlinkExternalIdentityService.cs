// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.ExternalSignIn.Unlink;

/// <summary>
///     The Service for a signed-in User to remove the link to their account at an external identity provider.
/// </summary>
/// <remarks>
///     Every User has a password in this version, so removing a link never leaves a User without a way to sign in.
/// </remarks>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to remove the link.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class UnlinkExternalIdentityService(
    IExternalIdentityRepository externalIdentityRepository,
    IAuditLog auditLog,
    ILogger<UnlinkExternalIdentityService> logger)
{
    /// <summary>
    ///     Removes a link of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="externalIdentityId">Id of the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the link was removed, false if the User has no such link.</returns>
    public async Task<bool> ExecuteAsync(Guid userId, Guid externalIdentityId, CancellationToken cancellationToken = default)
    {
        var identity = (await externalIdentityRepository.GetIdentitiesOfUser(userId, cancellationToken))
            .FirstOrDefault(identity => identity.Id == externalIdentityId);

        if (identity is null)
        {
            return false;
        }

        externalIdentityRepository.RemoveIdentities([identity]);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.ExternalIdentities.Unlinked,
            userId,
            $"Identity {identity.Id}, identity provider {identity.ProviderId}"));

        await externalIdentityRepository.SaveChangesAsync(cancellationToken);

        logger.ExternalIdentityUnlinked(userId, identity.Id, identity.ProviderId);

        return true;
    }
}
