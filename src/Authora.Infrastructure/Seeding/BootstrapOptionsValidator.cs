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
        }
        // A missing password is only an error while the administrator still has to be created, which IdentitySeeder
        // checks. Otherwise removing the password once an administrator exists would stop the next start.
        else if (!string.IsNullOrEmpty(administrator.Password)
            && administrator.Password is not { Length: >= PasswordPolicy.MinLength and <= PasswordPolicy.MaxLength })
        {
            failures.Add(
                $"'{section}:{nameof(BootstrapAdministratorOptions.Password)}' must be {PasswordPolicy.MinLength} to "
                + $"{PasswordPolicy.MaxLength} characters long.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
