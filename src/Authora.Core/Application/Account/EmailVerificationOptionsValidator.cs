// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account;

/// <summary>
///     Validates the <see cref="EmailVerificationOptions"/> at startup.
/// </summary>
public sealed class EmailVerificationOptionsValidator : IValidateOptions<EmailVerificationOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EmailVerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.LinkLifetime < EmailVerificationOptions.MinLinkLifetime || options.LinkLifetime > EmailVerificationOptions.MaxLinkLifetime)
        {
            return ValidateOptionsResult.Fail(
                $"'{EmailVerificationOptions.SectionName}:{nameof(EmailVerificationOptions.LinkLifetime)}' must be between "
                + $"{EmailVerificationOptions.MinLinkLifetime} and {EmailVerificationOptions.MaxLinkLifetime}. Use the format "
                + "d.hh:mm:ss, e.g. 1.00:00:00 or 02:00:00; a plain number is read as days.");
        }

        return ValidateOptionsResult.Success;
    }
}
