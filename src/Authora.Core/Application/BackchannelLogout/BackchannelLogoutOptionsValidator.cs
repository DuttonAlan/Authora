// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.BackchannelLogout;

/// <summary>
///     Validates the <see cref="BackchannelLogoutOptions"/> at startup.
/// </summary>
public sealed class BackchannelLogoutOptionsValidator : IValidateOptions<BackchannelLogoutOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BackchannelLogoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var section = BackchannelLogoutOptions.SectionName;

        if (options.Timeout < BackchannelLogoutOptions.MinTimeout || options.Timeout > BackchannelLogoutOptions.MaxTimeout)
        {
            failures.Add(
                $"'{section}:{nameof(BackchannelLogoutOptions.Timeout)}' must be between {BackchannelLogoutOptions.MinTimeout} and {BackchannelLogoutOptions.MaxTimeout}.");
        }

        if (options.MaxAttempts is < 1 or > BackchannelLogoutOptions.MaxMaxAttempts)
        {
            failures.Add(
                $"'{section}:{nameof(BackchannelLogoutOptions.MaxAttempts)}' must be between 1 and {BackchannelLogoutOptions.MaxMaxAttempts}.");
        }

        if (options.MaxAge < BackchannelLogoutOptions.MinMaxAge || options.MaxAge > BackchannelLogoutOptions.MaxMaxAge)
        {
            failures.Add(
                $"'{section}:{nameof(BackchannelLogoutOptions.MaxAge)}' must be between {BackchannelLogoutOptions.MinMaxAge} and {BackchannelLogoutOptions.MaxMaxAge}.");
        }

        if (options.PollInterval < BackchannelLogoutOptions.MinPollInterval || options.PollInterval > BackchannelLogoutOptions.MaxPollInterval)
        {
            failures.Add(
                $"'{section}:{nameof(BackchannelLogoutOptions.PollInterval)}' must be between {BackchannelLogoutOptions.MinPollInterval} and {BackchannelLogoutOptions.MaxPollInterval}.");
        }

        if (options.BatchSize is < 1 or > BackchannelLogoutOptions.MaxBatchSize)
        {
            failures.Add(
                $"'{section}:{nameof(BackchannelLogoutOptions.BatchSize)}' must be between 1 and {BackchannelLogoutOptions.MaxBatchSize}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
