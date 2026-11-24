// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     Validates the <see cref="ExternalSignInOptions"/> at startup.
/// </summary>
public sealed class ExternalSignInOptionsValidator : IValidateOptions<ExternalSignInOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ExternalSignInOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Timeout < ExternalSignInOptions.MinTimeout || options.Timeout > ExternalSignInOptions.MaxTimeout)
        {
            return ValidateOptionsResult.Fail(
                $"'{ExternalSignInOptions.SectionName}:{nameof(ExternalSignInOptions.Timeout)}' must be between {ExternalSignInOptions.MinTimeout} and {ExternalSignInOptions.MaxTimeout}.");
        }

        return ValidateOptionsResult.Success;
    }
}
