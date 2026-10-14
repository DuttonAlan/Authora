// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     Validates the <see cref="OidcOptions"/> at startup. The key files themselves are checked by the Infrastructure,
///     which loads them.
/// </summary>
public sealed class OidcOptionsValidator : IValidateOptions<OidcOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OidcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SigningKeyPath))
        {
            failures.Add(
                $"'{OidcOptions.SectionName}:{nameof(OidcOptions.SigningKeyPath)}' must be the path of a PEM file with an RSA "
                + "private key, e.g. via the environment variable 'Authora__Oidc__SigningKeyPath'. Create one with "
                + "'openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key.pem'.");
        }

        if (options.AdditionalKeyPaths.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"'{OidcOptions.SectionName}:{nameof(OidcOptions.AdditionalKeyPaths)}' must not contain empty paths.");
        }

        if (options.IdTokenLifetime <= TimeSpan.Zero || options.IdTokenLifetime > OidcOptions.MaxIdTokenLifetime)
        {
            failures.Add(
                $"'{OidcOptions.SectionName}:{nameof(OidcOptions.IdTokenLifetime)}' must be greater than 0 "
                + $"and at most {OidcOptions.MaxIdTokenLifetime}.");
        }

        if (options.KeyReloadInterval < OidcOptions.MinKeyReloadInterval || options.KeyReloadInterval > OidcOptions.MaxKeyReloadInterval)
        {
            failures.Add(
                $"'{OidcOptions.SectionName}:{nameof(OidcOptions.KeyReloadInterval)}' must be between "
                + $"{OidcOptions.MinKeyReloadInterval} and {OidcOptions.MaxKeyReloadInterval}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
