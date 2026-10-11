// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account;

/// <summary>
///     Validates the <see cref="SignInOptions"/> at startup.
/// </summary>
public sealed class SignInOptionsValidator : IValidateOptions<SignInOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SignInOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.SessionIdleTimeout <= TimeSpan.Zero)
        {
            failures.Add($"'{SignInOptions.SectionName}:{nameof(SignInOptions.SessionIdleTimeout)}' must be greater than 0.");
        }

        if (options.SessionLifetime < options.SessionIdleTimeout)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.SessionLifetime)}' must not be shorter than "
                + $"'{nameof(SignInOptions.SessionIdleTimeout)}'.");
        }

        if (options.MaxFailedAttempts < 1)
        {
            failures.Add($"'{SignInOptions.SectionName}:{nameof(SignInOptions.MaxFailedAttempts)}' must be at least 1.");
        }

        if (options.LockoutDuration <= TimeSpan.Zero)
        {
            failures.Add($"'{SignInOptions.SectionName}:{nameof(SignInOptions.LockoutDuration)}' must be greater than 0.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
