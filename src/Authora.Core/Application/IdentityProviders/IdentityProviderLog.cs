// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders;

/// <summary>
///     Log messages for changes to external identity providers. Client Secrets are never logged.
/// </summary>
internal static partial class IdentityProviderLog
{
    [LoggerMessage(EventId = 19001, Level = LogLevel.Information, Message = "Identity provider {ProviderId} ({ProviderSlug}) was created for Issuer {Issuer}.")]
    public static partial void IdentityProviderCreated(this ILogger logger, Guid providerId, string providerSlug, string issuer);

    [LoggerMessage(EventId = 19002, Level = LogLevel.Information, Message = "Identity provider {ProviderId} ({ProviderSlug}) was updated.")]
    public static partial void IdentityProviderUpdated(this ILogger logger, Guid providerId, string providerSlug);

    [LoggerMessage(EventId = 19003, Level = LogLevel.Warning, Message = "Identity provider {ProviderId} ({ProviderSlug}) was deleted.")]
    public static partial void IdentityProviderDeleted(this ILogger logger, Guid providerId, string providerSlug);

    [LoggerMessage(EventId = 19004, Level = LogLevel.Information, Message = "Identity provider {ProviderId} ({ProviderSlug}) was enabled.")]
    public static partial void IdentityProviderEnabled(this ILogger logger, Guid providerId, string providerSlug);

    [LoggerMessage(EventId = 19005, Level = LogLevel.Warning, Message = "Identity provider {ProviderId} ({ProviderSlug}) was disabled.")]
    public static partial void IdentityProviderDisabled(this ILogger logger, Guid providerId, string providerSlug);

    [LoggerMessage(EventId = 19006, Level = LogLevel.Information, Message = "The Client Secret of identity provider {ProviderId} ({ProviderSlug}) was {Change}.")]
    public static partial void IdentityProviderSecretChanged(this ILogger logger, Guid providerId, string providerSlug, string change);

    [LoggerMessage(EventId = 19007, Level = LogLevel.Warning, Message = "The Issuer {Issuer} was rejected: {Reason}")]
    public static partial void IssuerRejected(this ILogger logger, string issuer, string reason);
}
