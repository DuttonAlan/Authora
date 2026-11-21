// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account;

/// <summary>
///     Validates the <see cref="PasswordResetOptions"/> at startup.
/// </summary>
public sealed class PasswordResetOptionsValidator : IValidateOptions<PasswordResetOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, PasswordResetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var section = PasswordResetOptions.SectionName;

        if (options.LinkLifetime < PasswordResetOptions.MinLinkLifetime || options.LinkLifetime > PasswordResetOptions.MaxLinkLifetime)
        {
            failures.Add(
                $"'{section}:{nameof(PasswordResetOptions.LinkLifetime)}' must be between {PasswordResetOptions.MinLinkLifetime} "
                + $"and {PasswordResetOptions.MaxLinkLifetime}. Use the format hh:mm:ss, e.g. 00:30:00; a plain number is read as days.");
        }

        if (options.MaxEmailsPerHour is < 1 or > PasswordResetOptions.MaxEmailsPerHourLimit)
        {
            failures.Add(
                $"'{section}:{nameof(PasswordResetOptions.MaxEmailsPerHour)}' must be between 1 and {PasswordResetOptions.MaxEmailsPerHourLimit}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
