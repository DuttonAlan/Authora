// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.AdminUi;

/// <summary>
///     Settings of the admin interface at <see cref="Path"/> (US-033), bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     The admin interface is a browser app that signs in through Authora's own Authorization Code flow with PKCE as
///     a Public Client, and then calls the admin API with the Access Token, like every other Client.
/// </remarks>
public sealed class AdminUiOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:AdminUi";

    /// <summary>
    ///     The path the admin interface is served at, below the Issuer.
    /// </summary>
    public const string Path = "/admin";

    /// <summary>
    ///     Name of the Client the admin interface is created with.
    /// </summary>
    public const string ClientName = "Authora Admin UI";

    /// <summary>
    ///     Whether the admin interface is served, and its Client created at startup if no Client has its Redirect URI.
    ///     Defaults to true. When false, <see cref="Path"/> answers 404, and the Client is kept but not used.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets the Redirect URI of the admin interface, which identifies its Client.
    /// </summary>
    /// <param name="issuer">The Issuer, see <see cref="OAuth.OAuthOptions.Issuer"/>.</param>
    /// <returns>The Redirect URI, e.g. <c>https://auth.example.com/admin/</c>.</returns>
    public static string GetRedirectUri(string issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);

        return $"{issuer}{Path}/";
    }
}
