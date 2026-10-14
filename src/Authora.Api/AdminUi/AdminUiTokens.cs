// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.AdminUi;

/// <summary>
///     The tokens of the admin interface, kept in the encrypted cookie <see cref="AdminUiCookies.TokensName"/>.
/// </summary>
/// <param name="AccessToken">The Access Token.</param>
/// <param name="RefreshToken">The Refresh Token, or null if the Client gets none.</param>
/// <param name="IssuedAt">When the tokens were issued (UTC).</param>
/// <param name="SignedInAt">
///     When the User signed in to the admin interface (UTC). Refreshes keep it, so the tokens are not refreshed
///     beyond the lifetime of a Session. A cookie without it reads as long ago, so the User signs in again.
/// </param>
public sealed record AdminUiTokens(string AccessToken, string? RefreshToken, DateTimeOffset IssuedAt, DateTimeOffset SignedInAt)
{
    /// <summary>
    ///     Returns a description without the tokens, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the tokens.</returns>
    public override string ToString() => $"AdminUiTokens({IssuedAt:O}, {SignedInAt:O})";
}
