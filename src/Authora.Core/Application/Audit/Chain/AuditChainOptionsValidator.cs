// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Audit.Chain;

/// <summary>
///     Validates the <see cref="AuditChainOptions"/> at startup.
/// </summary>
public sealed class AuditChainOptionsValidator : IValidateOptions<AuditChainOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AuditChainOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var section = AuditChainOptions.SectionName;

        if (options.SealInterval < TimeSpan.FromSeconds(1) || options.SealInterval > AuditChainOptions.MaxSealInterval)
        {
            failures.Add(
                $"'{section}:{nameof(AuditChainOptions.SealInterval)}' must be between 00:00:01 and "
                + $"{AuditChainOptions.MaxSealInterval}. Use the format hh:mm:ss; a plain number is read as days.");
        }

        if (options.BatchSize is < 1 or > 10_000)
        {
            failures.Add($"'{section}:{nameof(AuditChainOptions.BatchSize)}' must be between 1 and 10000.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
