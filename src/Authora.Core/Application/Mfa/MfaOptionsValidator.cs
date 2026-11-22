// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Validates the <see cref="MfaOptions"/> at startup.
/// </summary>
public sealed class MfaOptionsValidator : IValidateOptions<MfaOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MfaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The colon separates the issuer from the account in the label of the otpauth:// URI.
        if (string.IsNullOrWhiteSpace(options.IssuerName)
            || options.IssuerName.Length > MfaOptions.IssuerNameMaxLength
            || options.IssuerName.Contains(':', StringComparison.Ordinal)
            || options.IssuerName.Any(char.IsControl))
        {
            return ValidateOptionsResult.Fail(
                $"'{MfaOptions.SectionName}:{nameof(MfaOptions.IssuerName)}' must be 1 to {MfaOptions.IssuerNameMaxLength} "
                + "characters long, without a colon or control characters.");
        }

        return ValidateOptionsResult.Success;
    }
}
