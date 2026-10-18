// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Clients;

/// <summary>
///     Log messages for changes to Clients. Secrets are never logged, only their Ids.
/// </summary>
internal static partial class ClientLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Client {ClientId} ({ClientName}) was registered as {ClientType} Client.")]
    public static partial void ClientRegistered(this ILogger logger, Guid clientId, string clientName, ClientType clientType);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Client {ClientId} ({ClientName}) was updated.")]
    public static partial void ClientUpdated(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Warning, Message = "Client {ClientId} ({ClientName}) was deleted.")]
    public static partial void ClientDeleted(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Client {ClientId} ({ClientName}) was enabled.")]
    public static partial void ClientEnabled(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "Client {ClientId} ({ClientName}) was disabled.")]
    public static partial void ClientDisabled(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "Redirect URI {RedirectUri} was added to Client {ClientId}.")]
    public static partial void RedirectUriAdded(this ILogger logger, Guid clientId, string redirectUri);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Information, Message = "Redirect URI {RedirectUri} was removed from Client {ClientId}.")]
    public static partial void RedirectUriRemoved(this ILogger logger, Guid clientId, string redirectUri);

    [LoggerMessage(EventId = 4008, Level = LogLevel.Information, Message = "Scope {ScopeId} ({ScopeName}) was allowed for Client {ClientId}.")]
    public static partial void ScopeAllowed(this ILogger logger, Guid clientId, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 4009, Level = LogLevel.Information, Message = "Scope {ScopeId} ({ScopeName}) was disallowed for Client {ClientId}.")]
    public static partial void ScopeDisallowed(this ILogger logger, Guid clientId, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 4010, Level = LogLevel.Information, Message = "Secret {SecretId} was created for Client {ClientId}, expires at {ExpiresAt}.")]
    public static partial void SecretCreated(this ILogger logger, Guid clientId, Guid secretId, DateTime expiresAt);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "Secret {SecretId} of Client {ClientId} was revoked.")]
    public static partial void SecretRevoked(this ILogger logger, Guid clientId, Guid secretId);

    [LoggerMessage(EventId = 4012, Level = LogLevel.Warning, Message = "{RevokedGrants} Grant(s) of Client {ClientId} were revoked, including their Access and Refresh Tokens.")]
    public static partial void ClientGrantsRevoked(this ILogger logger, Guid clientId, int revokedGrants);

    [LoggerMessage(EventId = 4013, Level = LogLevel.Information, Message = "Client {ClientId} now serves Scope {ScopeId} ({ScopeName}) as a resource server.")]
    public static partial void ScopeServed(this ILogger logger, Guid clientId, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 4014, Level = LogLevel.Information, Message = "Client {ClientId} no longer serves Scope {ScopeId} ({ScopeName}).")]
    public static partial void ScopeNoLongerServed(this ILogger logger, Guid clientId, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 4015, Level = LogLevel.Warning, Message = "Client {ClientId} ({ClientName}) was marked as first-party. Users are no longer asked for consent.")]
    public static partial void ClientMarkedFirstParty(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 4016, Level = LogLevel.Information, Message = "Client {ClientId} ({ClientName}) was marked as third-party.")]
    public static partial void ClientMarkedThirdParty(this ILogger logger, Guid clientId, string clientName);
}
