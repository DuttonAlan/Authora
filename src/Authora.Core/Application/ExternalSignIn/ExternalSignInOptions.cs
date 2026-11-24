// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     Settings of the sign-in through external OpenID Connect identity providers, bound from the section
///     <see cref="SectionName"/>. The providers themselves are managed through the admin API.
/// </summary>
public sealed class ExternalSignInOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:ExternalSignIn";

    /// <summary>
    ///     How long a User has from leaving for the provider until its response arrives.
    /// </summary>
    public static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Smallest allowed <see cref="Timeout"/>.
    /// </summary>
    public static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Largest allowed <see cref="Timeout"/>.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Whether providers may be reached over plain HTTP. Defaults to <see langword="false"/>: only HTTPS, because
    ///     the requests carry the Client Secret and the responses the identity of the User. Only for development;
    ///     Authora does not start with it outside the environment <c>Development</c>.
    /// </summary>
    public bool AllowHttp { get; set; }

    /// <summary>
    ///     How long a request to a provider (discovery, keys, token) may take. Defaults to 10 seconds, between
    ///     <see cref="MinTimeout"/> and <see cref="MaxTimeout"/>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Gets the path that starts the sign-in through a provider.
    /// </summary>
    /// <param name="slug">The Slug of the provider.</param>
    /// <returns>The path, e.g. <c>/account/external/google</c>.</returns>
    public static string GetStartPath(string slug)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return $"/account/external/{slug}";
    }

    /// <summary>
    ///     Gets the Redirect URI of Authora for a provider, which the administrator registers at the provider.
    /// </summary>
    /// <param name="issuer">The Issuer of Authora, see <see cref="OAuth.OAuthOptions.Issuer"/>.</param>
    /// <param name="slug">The Slug of the provider.</param>
    /// <returns>The Redirect URI, e.g. <c>https://auth.example.com/account/external/google/callback</c>.</returns>
    public static string GetRedirectUri(string issuer, string slug)
    {
        ArgumentNullException.ThrowIfNull(issuer);

        return $"{issuer}{GetStartPath(slug)}/callback";
    }
}
