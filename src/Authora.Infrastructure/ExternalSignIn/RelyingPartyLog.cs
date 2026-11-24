// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.ExternalSignIn;

/// <summary>
///     Log messages of the requests to external identity providers. Codes, tokens and secrets are never logged.
/// </summary>
internal static partial class RelyingPartyLog
{
    [LoggerMessage(EventId = 20021, Level = LogLevel.Warning, Message = "The discovery document or the key set of Issuer {Issuer} could not be fetched or read.")]
    public static partial void ProviderUnreachable(this ILogger logger, string issuer, Exception exception);

    [LoggerMessage(EventId = 20022, Level = LogLevel.Warning, Message = "The token endpoint of Issuer {Issuer} could not be reached.")]
    public static partial void TokenRequestFailed(this ILogger logger, string issuer, Exception exception);
}
