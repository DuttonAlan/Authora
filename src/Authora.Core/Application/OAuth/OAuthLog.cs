// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Log messages of the OAuth endpoints. Codes, tokens and Secrets are never logged, only their Ids.
/// </summary>
internal static partial class OAuthLog
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Authorization Code {AuthorizationCodeId} was issued to Client {ClientId} for User {UserId} with Scopes '{Scope}'.")]
    public static partial void AuthorizationCodeIssued(this ILogger logger, Guid authorizationCodeId, Guid clientId, Guid userId, string scope);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information, Message = "Grant {GrantId} was created for Client {ClientId} and User {UserId} from Authorization Code {AuthorizationCodeId}.")]
    public static partial void GrantCreated(this ILogger logger, Guid grantId, Guid clientId, Guid userId, Guid authorizationCodeId);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Tokens of Grant {GrantId} were refreshed for Client {ClientId}.")]
    public static partial void TokensRefreshed(this ILogger logger, Guid grantId, Guid clientId);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Warning, Message = "Authorization Code {AuthorizationCodeId} was used more than once. {RevokedGrants} Grant(s) issued from it were revoked.")]
    public static partial void AuthorizationCodeReused(this ILogger logger, Guid authorizationCodeId, int revokedGrants);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Warning, Message = "Refresh Token {RefreshTokenId} of Grant {GrantId} was used more than once. The Grant was revoked.")]
    public static partial void RefreshTokenReused(this ILogger logger, Guid refreshTokenId, Guid grantId);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Warning, Message = "Client {ClientId} presented a code or token that was issued to Client {OwnerClientId}.")]
    public static partial void TokenOfOtherClient(this ILogger logger, Guid clientId, Guid ownerClientId);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Warning, Message = "Authentication of Client {ClientId} failed: {Reason}")]
    public static partial void ClientAuthenticationFailed(this ILogger logger, string clientId, string reason);

    [LoggerMessage(EventId = 6008, Level = LogLevel.Information, Message = "Access Token {AccessTokenId} was revoked by Client {ClientId}.")]
    public static partial void AccessTokenRevoked(this ILogger logger, Guid accessTokenId, Guid clientId);

    [LoggerMessage(EventId = 6009, Level = LogLevel.Information, Message = "Grant {GrantId} was revoked by Client {ClientId} through one of its Refresh Tokens.")]
    public static partial void GrantRevokedByClient(this ILogger logger, Guid grantId, Guid clientId);

    [LoggerMessage(EventId = 6010, Level = LogLevel.Information, Message = "Grant {GrantId} was revoked, because User {UserId} can no longer use it.")]
    public static partial void GrantRevokedForUser(this ILogger logger, Guid grantId, Guid userId);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Information, Message = "Authorization request of Client {ClientId} was rejected: {Error}.")]
    public static partial void AuthorizationRequestRejected(this ILogger logger, string clientId, string error);
}
