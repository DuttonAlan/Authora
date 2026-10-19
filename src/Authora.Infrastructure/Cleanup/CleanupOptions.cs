// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Settings of the cleanup of expired Authorization Codes, Grants, tokens, Sessions and audit events, bound from the section
///     <see cref="SectionName"/>.
/// </summary>
public sealed class CleanupOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Cleanup";

    /// <summary>
    ///     Minimum of <see cref="Retention"/>.
    /// </summary>
    public static readonly TimeSpan MinRetention = TimeSpan.FromDays(1);

    /// <summary>
    ///     Minimum of <see cref="AuditRetention"/>.
    /// </summary>
    public static readonly TimeSpan MinAuditRetention = TimeSpan.FromDays(30);

    /// <summary>
    ///     Maximum of <see cref="BatchSize"/>.
    /// </summary>
    public const int MaxBatchSize = 10_000;

    /// <summary>
    ///     How often the cleanup runs. Defaults to 1 hour, between 1 minute and 1 day.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    ///     How long rows are kept after they expired or were revoked. Defaults to 7 days, at least
    ///     <see cref="MinRetention"/>.
    /// </summary>
    /// <remarks>
    ///     A consumed Authorization Code or Refresh Token that is used again revokes its Grant, also after it expired.
    ///     Once the row is deleted, such a reuse is only rejected, so the Grant stays valid.
    /// </remarks>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    ///     How many rows are deleted per statement, so a large backlog does not lock a table for long.
    ///     Defaults to 1000, at most <see cref="MaxBatchSize"/>.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    ///     How long audit events are kept. Defaults to 365 days, at least <see cref="MinAuditRetention"/>.
    /// </summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(365);
}
