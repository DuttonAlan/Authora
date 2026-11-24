// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     Log messages of the sign-in through external identity providers and of linked identities. Codes, tokens,
///     secrets, <c>state</c> and <c>nonce</c> are never logged; Subjects of the provider neither.
/// </summary>
internal static partial class ExternalSignInLog
{
    [LoggerMessage(EventId = 20001, Level = LogLevel.Information, Message = "A sign-in through identity provider {ProviderSlug} started.")]
    public static partial void ExternalSignInStarted(this ILogger logger, string providerSlug);

    [LoggerMessage(EventId = 20002, Level = LogLevel.Information, Message = "User {UserId} started linking an account at identity provider {ProviderSlug}.")]
    public static partial void ExternalLinkStarted(this ILogger logger, Guid userId, string providerSlug);

    [LoggerMessage(EventId = 20003, Level = LogLevel.Warning, Message = "A sign-in through identity provider {ProviderSlug} could not start: {Reason}")]
    public static partial void ExternalSignInNotStarted(this ILogger logger, string providerSlug, string reason);

    [LoggerMessage(EventId = 20004, Level = LogLevel.Information, Message = "User {UserId} signed in through identity provider {ProviderSlug} with Session {SessionId}.")]
    public static partial void ExternalSignedIn(this ILogger logger, Guid userId, string providerSlug, Guid sessionId);

    [LoggerMessage(EventId = 20005, Level = LogLevel.Information, Message = "User {UserId} signed in through identity provider {ProviderSlug} and must complete a second factor for pending sign-in {ChallengeId}.")]
    public static partial void ExternalSignInPending(this ILogger logger, Guid userId, string providerSlug, Guid challengeId);

    [LoggerMessage(EventId = 20006, Level = LogLevel.Warning, Message = "A sign-in through identity provider {ProviderSlug} failed: {Reason}")]
    public static partial void ExternalSignInFailed(this ILogger logger, string providerSlug, string reason);

    [LoggerMessage(EventId = 20008, Level = LogLevel.Information, Message = "User {UserId} linked identity {ExternalIdentityId} at identity provider {ProviderSlug}.")]
    public static partial void ExternalIdentityLinked(this ILogger logger, Guid userId, Guid externalIdentityId, string providerSlug);

    [LoggerMessage(EventId = 20009, Level = LogLevel.Information, Message = "User {UserId} removed identity {ExternalIdentityId} at identity provider {ProviderId}.")]
    public static partial void ExternalIdentityUnlinked(this ILogger logger, Guid userId, Guid externalIdentityId, Guid providerId);

    [LoggerMessage(EventId = 20010, Level = LogLevel.Information, Message = "User {UserId} must set up a second factor after signing in through an identity provider.")]
    public static partial void SecondFactorEnrollmentRequired(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 20011, Level = LogLevel.Information, Message = "An account at identity provider {ProviderSlug} that is not linked has the Email of User {UserId}, who must sign in to link it.")]
    public static partial void ExternalLinkRequired(this ILogger logger, Guid userId, string providerSlug);

    [LoggerMessage(EventId = 20012, Level = LogLevel.Information, Message = "An account at identity provider {ProviderSlug} that is not linked cannot sign in: {Reason}")]
    public static partial void ExternalSignUpRefused(this ILogger logger, string providerSlug, string reason);

    [LoggerMessage(EventId = 20013, Level = LogLevel.Information, Message = "User {UserId} was created on their first sign-in through identity provider {ProviderSlug}, with identity {ExternalIdentityId}.")]
    public static partial void UserProvisioned(this ILogger logger, Guid userId, string providerSlug, Guid externalIdentityId);

    [LoggerMessage(EventId = 20014, Level = LogLevel.Warning, Message = "A pending link of an account at identity provider {ProviderSlug} was not completed: {Reason}")]
    public static partial void PendingLinkRejected(this ILogger logger, string providerSlug, string reason);

    [LoggerMessage(EventId = 20015, Level = LogLevel.Information, Message = "User {UserId} cannot remove identity {ExternalIdentityId}: it is their last way to sign in.")]
    public static partial void LastSignInMethodKept(this ILogger logger, Guid userId, Guid externalIdentityId);

    [LoggerMessage(EventId = 20016, Level = LogLevel.Information, Message = "An administrator removed identity {ExternalIdentityId} of User {UserId} at identity provider {ProviderId}.")]
    public static partial void ExternalIdentityRemovedByAdministrator(this ILogger logger, Guid userId, Guid externalIdentityId, Guid providerId);
}
