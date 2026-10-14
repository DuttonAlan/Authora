// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.OAuth;

namespace Authora.Api.Security;

/// <summary>
///     Checks at startup that a Production environment is configured securely (US-036). Authora does not start
///     otherwise, so an insecure setting cannot go unnoticed.
/// </summary>
/// <remarks>
///     Only settings that are always wrong in Production are checked. Further requirements are described in
///     <c>docs/operations.md</c>.
/// </remarks>
internal static class ProductionSettingsCheck
{
    /// <summary>
    ///     Checks the configuration.
    /// </summary>
    /// <param name="configuration">The configuration of the host.</param>
    /// <returns>The error messages, empty if the configuration is secure enough.</returns>
    public static IReadOnlyList<string> Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var failures = new List<string>();

        // Links, Redirect URIs and the iss claim are built from the Issuer, so it must not be plain HTTP. Loopback
        // HTTP is only allowed for development.
        var issuer = configuration[$"{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}"];

        if (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) && issuerUri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must use HTTPS in Production.");
        }

        // Host filtering rejects requests for other host names, e.g. a forged Host header that ends up in a link.
        var allowedHosts = configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
        {
            failures.Add(
                "'AllowedHosts' must list the host names of Authora in Production, e.g. 'auth.example.com', not '*'.");
        }

        return failures;
    }
}
