// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Oidc;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Loads the key files of the <see cref="OidcOptions"/> at startup and whenever the settings change, so a missing
///     or invalid key is reported right away instead of on the first sign-in.
/// </summary>
/// <param name="environment">The host environment, whose content root relative paths are resolved against.</param>
public sealed class OidcSigningKeysValidator(IHostEnvironment environment) : IValidateOptions<OidcOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OidcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Reported by OidcOptionsValidator.
        if (string.IsNullOrWhiteSpace(options.SigningKeyPath) || options.AdditionalKeyPaths.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Skip;
        }

        try
        {
            using var _ = SigningKeySet.Parse(SigningKeySet.ReadFiles(options, environment.ContentRootPath));
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail($"'{OidcOptions.SectionName}': {exception.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
