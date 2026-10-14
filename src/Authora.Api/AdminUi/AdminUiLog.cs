// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.AdminUi;

/// <summary>
///     Log messages of the backend for frontend of the admin interface. Tokens and codes are never logged.
/// </summary>
internal static partial class AdminUiLog
{
    [LoggerMessage(EventId = 12001, Level = LogLevel.Warning, Message = "Signing in to the admin interface failed: {Error}.")]
    public static partial void AdminUiSignInFailed(this ILogger logger, string error);

    [LoggerMessage(EventId = 12002, Level = LogLevel.Information, Message = "Refreshing the tokens of the admin interface failed: {Error}. The User must sign in again.")]
    public static partial void AdminUiRefreshFailed(this ILogger logger, string error);
}
