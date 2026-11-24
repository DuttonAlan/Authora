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
///     A User without a password cannot remove their last link, which is then their only way to sign in; they set a
///     password first. The check runs under the lock of the User, so two removals at the same time cannot both pass it.
///     Existing Sessions stay.
/// </remarks>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to remove the link.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check whether the User has a password.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository for the lock of the User's ways to sign in.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class UnlinkExternalIdentityService(
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    IAuditLog auditLog,
    ILogger<UnlinkExternalIdentityService> logger)
{
    /// <summary>
    ///     Removes a link of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="externalIdentityId">Id of the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the link was removed, or why not.</returns>
    public async Task<UnlinkExternalIdentityResult> ExecuteAsync(Guid userId, Guid externalIdentityId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await mfaRepository.BeginMfaChangeAsync(userId, cancellationToken);

        var user = await userRepository.GetUserById(userId, cancellationToken);
        var identities = await externalIdentityRepository.GetIdentitiesOfUser(userId, cancellationToken);
        var identity = identities.FirstOrDefault(identity => identity.Id == externalIdentityId);

        if (user is null || identity is null)
        {
            return UnlinkExternalIdentityResult.NotFound;
        }

        if (IsLastSignInMethod(user.HasPassword, identities.Count))
        {
            logger.LastSignInMethodKept(userId, identity.Id);

            return UnlinkExternalIdentityResult.LastSignInMethod;
        }

        externalIdentityRepository.RemoveIdentities([identity]);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.ExternalIdentities.Unlinked,
            userId,
            $"Identity {identity.Id}, identity provider {identity.ProviderId}, by the User"));

        await externalIdentityRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.ExternalIdentityUnlinked(userId, identity.Id, identity.ProviderId);

        return UnlinkExternalIdentityResult.Unlinked;
    }

    /// <summary>
    ///     Checks whether removing one of the User's links would leave them without a way to sign in: they have no
    ///     password and no other link.
    /// </summary>
    /// <param name="hasPassword">Whether the User has a password.</param>
    /// <param name="linkCount">How many accounts are linked to the User, including the one to remove.</param>
    /// <returns>True if the link must be kept.</returns>
    internal static bool IsLastSignInMethod(bool hasPassword, int linkCount) => !hasPassword && linkCount <= 1;
}

/// <summary>
///     The outcome of <see cref="UnlinkExternalIdentityService"/>.
/// </summary>
public enum UnlinkExternalIdentityResult
{
    /// <summary>
    ///     The link was removed.
    /// </summary>
    Unlinked,

    /// <summary>
    ///     The User has no such link.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The link was kept: the User has no password and no other link, so it is their only way to sign in.
    /// </summary>
    LastSignInMethod
}
