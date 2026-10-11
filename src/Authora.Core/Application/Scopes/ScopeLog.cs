// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Scopes;

/// <summary>
///     Log messages for changes to Scopes.
/// </summary>
internal static partial class ScopeLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Scope {ScopeId} ({ScopeName}) was created.")]
    public static partial void ScopeCreated(this ILogger logger, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Scope {ScopeId} ({ScopeName}) was updated.")]
    public static partial void ScopeUpdated(this ILogger logger, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Scope {ScopeId} ({ScopeName}) was deleted and removed from {ClientCount} Clients.")]
    public static partial void ScopeDeleted(this ILogger logger, Guid scopeId, string scopeName, int clientCount);
}
