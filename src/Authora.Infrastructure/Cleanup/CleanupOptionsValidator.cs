// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Validates the <see cref="CleanupOptions"/> at startup.
/// </summary>
public sealed class CleanupOptionsValidator : IValidateOptions<CleanupOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, CleanupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var section = CleanupOptions.SectionName;

        if (options.Interval < TimeSpan.FromMinutes(1) || options.Interval > TimeSpan.FromDays(1))
        {
            failures.Add($"'{section}:{nameof(CleanupOptions.Interval)}' must be between 00:01:00 and 1.00:00:00.");
        }

        if (options.Retention < CleanupOptions.MinRetention)
        {
            failures.Add($"'{section}:{nameof(CleanupOptions.Retention)}' must be at least {CleanupOptions.MinRetention}.");
        }

        if (options.AuditRetention < CleanupOptions.MinAuditRetention)
        {
            failures.Add($"'{section}:{nameof(CleanupOptions.AuditRetention)}' must be at least {CleanupOptions.MinAuditRetention}.");
        }

        if (options.BatchSize is < 1 or > CleanupOptions.MaxBatchSize)
        {
            failures.Add($"'{section}:{nameof(CleanupOptions.BatchSize)}' must be between 1 and {CleanupOptions.MaxBatchSize}.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
