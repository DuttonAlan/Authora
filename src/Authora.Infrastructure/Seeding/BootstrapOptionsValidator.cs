// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Users.SetPassword;

using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Seeding;

/// <summary>
///     Validates the <see cref="BootstrapOptions"/> at startup.
/// </summary>
public sealed class BootstrapOptionsValidator : IValidateOptions<BootstrapOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BootstrapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var administrator = options.Administrator;
        var section = $"{BootstrapOptions.SectionName}:{nameof(BootstrapOptions.Administrator)}";

        if (string.IsNullOrWhiteSpace(administrator.Email))
        {
            if (!string.IsNullOrEmpty(administrator.Password))
            {
                failures.Add($"'{section}:{nameof(BootstrapAdministratorOptions.Email)}' must be set if a password is set.");
            }

            if (options.Client.RedirectUris.Count > 0)
            {
                failures.Add(
                    $"'{BootstrapOptions.SectionName}:{nameof(BootstrapOptions.Client)}' is only created together with the "
                    + $"administrator, so '{section}:{nameof(BootstrapAdministratorOptions.Email)}' must be set.");
            }
        }
        else if (administrator.Password is not { Length: >= PasswordPolicy.MinLength and <= PasswordPolicy.MaxLength })
        {
            failures.Add(
                $"'{section}:{nameof(BootstrapAdministratorOptions.Password)}' must be {PasswordPolicy.MinLength} to "
                + $"{PasswordPolicy.MaxLength} characters long.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
