// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.OAuth;

/// <summary>
///     Validates the <see cref="OAuthOptions"/> at startup, so a missing or invalid Issuer is reported right away.
/// </summary>
public sealed class OAuthOptionsValidator : IValidateOptions<OAuthOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer))
        {
            failures.Add(
                $"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must be an absolute URL, "
                + "e.g. via the environment variable 'Authora__OAuth__Issuer'.");
        }
        else
        {
            if (issuer.Scheme != Uri.UriSchemeHttps && !(issuer.Scheme == Uri.UriSchemeHttp && issuer.IsLoopback))
            {
                failures.Add($"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must use HTTPS (HTTP only for loopback hosts).");
            }

            if (!string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment) || !string.IsNullOrEmpty(issuer.UserInfo))
            {
                failures.Add($"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must not contain a query, fragment or user information.");
            }

            // Clients append /.well-known/openid-configuration and compare iss character by character (OpenID Connect
            // Discovery 1.0, Sections 4 and 4.3), so "https://auth.example.com/" would not match.
            if (options.Issuer.EndsWith('/'))
            {
                failures.Add($"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.Issuer)}' must not end with '/'.");
            }
        }

        if (options.AuthorizationCodeLifetime <= TimeSpan.Zero || options.AuthorizationCodeLifetime > AuthorizationCode.MaxLifetime)
        {
            failures.Add(
                $"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.AuthorizationCodeLifetime)}' must be greater than 0 "
                + $"and at most {AuthorizationCode.MaxLifetime}.");
        }

        if (options.CorsOriginsCacheDuration < TimeSpan.Zero || options.CorsOriginsCacheDuration > OAuthOptions.MaxCorsOriginsCacheDuration)
        {
            failures.Add(
                $"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.CorsOriginsCacheDuration)}' must be at least 0 "
                + $"and at most {OAuthOptions.MaxCorsOriginsCacheDuration}.");
        }

        if (options.GrantLifetime <= TimeSpan.Zero || options.GrantLifetime > OAuthOptions.MaxGrantLifetime)
        {
            failures.Add(
                $"'{OAuthOptions.SectionName}:{nameof(OAuthOptions.GrantLifetime)}' must be greater than 0 "
                + $"and at most {OAuthOptions.MaxGrantLifetime}. Use the format d.hh:mm:ss, e.g. 90.00:00:00.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
