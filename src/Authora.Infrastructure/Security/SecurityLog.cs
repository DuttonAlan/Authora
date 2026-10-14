// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Log messages of the key handling. Key material is never logged, only the <c>kid</c>.
/// </summary>
internal static partial class SecurityLog
{
    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "ID Tokens are signed with key {KeyId}. {PublishedKeys} key(s) are published in the JWKS.")]
    public static partial void SigningKeysLoaded(this ILogger logger, string keyId, int publishedKeys);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "The key files changed. ID Tokens are now signed with key {KeyId}. {PublishedKeys} key(s) are published in the JWKS.")]
    public static partial void SigningKeysReloaded(this ILogger logger, string keyId, int publishedKeys);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Error, Message = "The changed key files could not be loaded. The previous keys stay in use until the files are fixed.")]
    public static partial void SigningKeysReloadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9004, Level = LogLevel.Warning, Message = "The signing key file {Path} can be read or written by other users of the system. Allow only the user that runs Authora, e.g. with 'chmod 600'.")]
    public static partial void SigningKeyFileReadableByOthers(this ILogger logger, string path);

    [LoggerMessage(EventId = 9005, Level = LogLevel.Warning, Message = "The change of the signing keys could not be recorded in the audit log. It is retried with the next check.")]
    public static partial void SigningKeysAuditFailed(this ILogger logger, Exception exception);
}
