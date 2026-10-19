// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit.Chain;

/// <summary>
///     Settings of the hash chain of the audit log, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class AuditChainOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Audit";

    /// <summary>
    ///     Maximum of <see cref="SealInterval"/>.
    /// </summary>
    public static readonly TimeSpan MaxSealInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     How far back the sealing looks for events that were committed late, e.g. by a long transaction or an
    ///     instance with a clock that is behind. Older events that are not sealed yet are reported by the verification.
    /// </summary>
    public static readonly TimeSpan LateEventWindow = TimeSpan.FromHours(1);

    /// <summary>
    ///     How often new events are sealed into the chain. Until then, an event can be changed without a trace.
    ///     Defaults to 5 seconds, between 1 second and <see cref="MaxSealInterval"/>.
    /// </summary>
    public TimeSpan SealInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How many events are sealed or verified per batch. Defaults to 1000, between 1 and 10000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
