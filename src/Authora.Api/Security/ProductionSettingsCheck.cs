// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.OAuth;

namespace Authora.Api.Security;

/// <summary>
///     Checks at startup that a deployed environment is configured securely (US-036). Authora does not start
///     otherwise, so an insecure setting cannot go unnoticed.
/// </summary>
/// <remarks>
///     Only settings that are always wrong outside development are checked. Further requirements are described in
///     <c>docs/operations.md</c>.
/// </remarks>
internal static class ProductionSettingsCheck
{
    /// <summary>
    ///     The environment of the integration tests, which run with a loopback Issuer like Development.
    /// </summary>
    public const string TestingEnvironment = "Testing";

    /// <summary>
    ///     Whether the check applies to an environment: to every environment except Development and Testing. Listing
    ///     the exceptions instead of Production means an environment named e.g. <c>Staging</c> or <c>Prod</c> is
    ///     checked too, instead of silently starting with insecure settings.
    /// </summary>
    /// <param name="environment">The environment of the host.</param>
    /// <returns><see langword="true"/> if <see cref="Validate"/> must run.</returns>
    public static bool AppliesTo(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return !environment.IsDevelopment() && !environment.IsEnvironment(TestingEnvironment);
    }

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
            failures.Add($"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must use HTTPS outside Development.");
        }

        // Host filtering rejects requests for other host names, e.g. a forged Host header that ends up in a link.
        var allowedHosts = configuration["AllowedHosts"];

        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
        {
            failures.Add(
                "'AllowedHosts' must list the host names of Authora outside Development, e.g. 'auth.example.com', not '*'.");
        }

        return failures;
    }
}
