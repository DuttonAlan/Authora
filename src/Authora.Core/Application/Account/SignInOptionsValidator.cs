// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account;

/// <summary>
///     Validates the <see cref="SignInOptions"/> at startup.
/// </summary>
/// <remarks>
///     Durations have upper limits, because a plain number like <c>15</c> is read as days, not minutes. Without a
///     limit, such a mistake would silently lock Users out for days or keep Sessions alive for weeks.
/// </remarks>
public sealed class SignInOptionsValidator : IValidateOptions<SignInOptions>
{
    private const string DurationHint = "Use the format hh:mm:ss, e.g. 00:15:00; a plain number is read as days.";

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SignInOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.SessionIdleTimeout <= TimeSpan.Zero || options.SessionIdleTimeout > SignInOptions.MaxSessionIdleTimeout)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.SessionIdleTimeout)}' must be greater than 0 "
                + $"and at most {SignInOptions.MaxSessionIdleTimeout}. {DurationHint}");
        }

        if (options.SessionLifetime < options.SessionIdleTimeout)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.SessionLifetime)}' must not be shorter than "
                + $"'{nameof(SignInOptions.SessionIdleTimeout)}'.");
        }

        if (options.SessionLifetime > SignInOptions.MaxSessionLifetime)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.SessionLifetime)}' must be at most "
                + $"{SignInOptions.MaxSessionLifetime}. {DurationHint}");
        }

        if (options.MaxFailedAttempts < 1 || options.MaxFailedAttempts > SignInOptions.MaxFailedAttemptsLimit)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.MaxFailedAttempts)}' must be between 1 "
                + $"and {SignInOptions.MaxFailedAttemptsLimit}.");
        }

        if (options.LockoutDuration <= TimeSpan.Zero || options.LockoutDuration > SignInOptions.MaxLockoutDuration)
        {
            failures.Add(
                $"'{SignInOptions.SectionName}:{nameof(SignInOptions.LockoutDuration)}' must be greater than 0 "
                + $"and at most {SignInOptions.MaxLockoutDuration}. {DurationHint}");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
