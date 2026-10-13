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
}
