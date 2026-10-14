// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Api.RateLimiting;

/// <summary>
///     Validates the <see cref="RateLimitOptions"/> at startup.
/// </summary>
public sealed class RateLimitOptionsValidator : IValidateOptions<RateLimitOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, RateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var section = RateLimitOptions.SectionName;

        if (options.SignInPermitsPerMinute is < 1 or > RateLimitOptions.MaxPermitsPerMinute)
        {
            failures.Add($"'{section}:{nameof(RateLimitOptions.SignInPermitsPerMinute)}' must be between 1 and {RateLimitOptions.MaxPermitsPerMinute}.");
        }

        if (options.TokenPermitsPerMinute is < 1 or > RateLimitOptions.MaxPermitsPerMinute)
        {
            failures.Add($"'{section}:{nameof(RateLimitOptions.TokenPermitsPerMinute)}' must be between 1 and {RateLimitOptions.MaxPermitsPerMinute}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
