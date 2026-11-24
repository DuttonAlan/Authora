// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.ExternalSignIn.CompleteLink;

/// <summary>
///     The Service that links an account at an external identity provider whose verified Email belongs to a User
///     (a <see cref="PendingExternalLink"/>), once the browser signed in to exactly that User with the User's existing
///     methods.
/// </summary>
/// <remarks>
///     <para>
///         The Session must have started after the account arrived, so a browser that was already signed in cannot
///         link without signing in again, not even after a step-up of its earlier Session; and it must be a Session of the User with the Email, so the account
///         is never linked to someone else. A User with a second factor gets a Session only after completing it, so
///         the link also requires the second factor.
///     </para>
///     <para>
///         The link is recorded as <see cref="AuditEventTypes.ExternalIdentities.Linked"/> with <c>via sign-in
///         prompt</c>; refusals as <see cref="AuditEventTypes.Account.ExternalSignInFailed"/>.
///     </para>
/// </remarks>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the provider.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to create the link.</param>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository for the lock of the User that a link shares with deleting the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to check the Session.</param>
/// <param name="timeProvider">The TimeProvider for the lifetime of the pending link and the link.</param>
/// <param name="signInOptions">The sign-in settings with the idle timeout of Sessions.</param>
/// <param name="auditLog">The audit log to record the link and refusals.</param>
/// <param name="logger">The Logger to record the link and refusals.</param>
public sealed class CompletePendingLinkService(
    IIdentityProviderRepository providerRepository,
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    ISessionRepository sessionRepository,
    TimeProvider timeProvider,
    IOptions<SignInOptions> signInOptions,
    IAuditLog auditLog,
    ILogger<CompletePendingLinkService> logger)
{
    /// <summary>
    ///     Links the pending account to the signed-in User.
    /// </summary>
    /// <param name="pending">The pending link from the cookie of the browser, or null if it has none or it expired.</param>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session of the browser.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The outcome.</returns>
    public async Task<CompletePendingLinkResult> ExecuteAsync(
        PendingExternalLink? pending,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (pending is null || now - pending.CreatedAt > ExternalSignInOptions.PendingLinkLifetime)
        {
            return CompletePendingLinkResult.Failed;
        }

        var session = await sessionRepository.GetSessionById(sessionId, cancellationToken);
        var user = await userRepository.GetUserById(userId, cancellationToken);

        if (session is null
            || user is null
            || session.UserId != user.Id
            || !user.CanSignIn
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, signInOptions.Value.SessionIdleTimeout))
        {
            logger.PendingLinkRejected(pending.Slug, "the Session ended");

            return CompletePendingLinkResult.Failed;
        }

        if (user.Id != pending.UserId)
        {
            return await RefuseAsync(
                pending,
                pending.UserId,
                "The browser signed in to another User than the one with the Email of the account",
                CompletePendingLinkResult.WrongUser,
                cancellationToken);
        }

        // The sign-in that proves the browser can use the User's existing methods must come after the account arrived.
        // CreatedAt, not AuthenticatedAt: a step-up moves AuthenticatedAt of an earlier Session without proving the
        // password again, so only a Session started by a full sign-in after the account arrived counts.
        if (session.CreatedAt < pending.CreatedAt)
        {
            return CompletePendingLinkResult.SignInRequired;
        }

        var provider = await providerRepository.GetProviderById(pending.ProviderId, cancellationToken);

        if (provider is null || !provider.IsEnabled || !string.Equals(provider.Slug, pending.Slug, StringComparison.Ordinal))
        {
            return await RefuseAsync(pending, user.Id, "The provider was disabled or deleted", CompletePendingLinkResult.Failed, cancellationToken);
        }

        // The lock that deleting the User holds while it removes the links, see CompleteExternalSignInService.
        await using var transaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        if (await userRepository.IsUserDeleted(user.Id, cancellationToken))
        {
            return await RefuseAsync(pending, user.Id, "The User was deleted", CompletePendingLinkResult.Failed, cancellationToken);
        }

        var existing = await externalIdentityRepository.FindIdentity(provider.Id, pending.Subject, cancellationToken);

        if (existing is not null)
        {
            return existing.UserId == user.Id
                ? CompletePendingLinkResult.AlreadyLinked
                : await RefuseAsync(
                    pending,
                    user.Id,
                    "The account at the provider was linked to another User in the meantime",
                    CompletePendingLinkResult.LinkConflict,
                    cancellationToken);
        }

        var identity = ExternalIdentity.Create(user.Id, provider.Id, pending.Subject, pending.Email, now);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.ExternalIdentities.Linked,
            user.Id,
            $"Identity {identity.Id}, identity provider '{provider.Slug}' ({provider.Id}), via sign-in prompt"));

        // Fails if the User already has an account at the provider, or another request linked the same account.
        if (!await externalIdentityRepository.TryCreateIdentity(identity, cancellationToken))
        {
            return await RefuseAsync(
                pending,
                user.Id,
                "The User already has an account at the provider linked, or the account was linked at the same time",
                CompletePendingLinkResult.LinkConflict,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.ExternalIdentityLinked(user.Id, identity.Id, provider.Slug);

        return CompletePendingLinkResult.Linked;
    }

    private async Task<CompletePendingLinkResult> RefuseAsync(
        PendingExternalLink pending,
        Guid userId,
        string reason,
        CompletePendingLinkResult result,
        CancellationToken cancellationToken)
    {
        logger.PendingLinkRejected(pending.Slug, reason);

        var entry = AuditEntry.ForUser(
            AuditEventTypes.Account.ExternalSignInFailed,
            userId,
            $"Identity provider '{pending.Slug}' ({pending.ProviderId}): {reason}");

        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);

        return result;
    }
}
