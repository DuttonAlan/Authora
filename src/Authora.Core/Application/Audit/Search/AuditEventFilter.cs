// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Audit.Search;

/// <summary>
///     The criteria to search audit events. Criteria that are null are not applied, all others must match.
/// </summary>
/// <param name="Type">
///     A Type (e.g. <c>user.created</c>) or an area (e.g. <c>user</c>, which matches all <c>user.*</c> events).
/// </param>
/// <param name="ActorUserId">Only events caused by this User.</param>
/// <param name="TargetType">Only events about this kind of object, e.g. <c>user</c>.</param>
/// <param name="TargetId">Only events about the object with this Id.</param>
/// <param name="Outcome">Only events with this outcome.</param>
/// <param name="From">Only events that occurred at or after this point in time.</param>
/// <param name="To">Only events that occurred before this point in time.</param>
public sealed record AuditEventFilter(
    string? Type = null,
    Guid? ActorUserId = null,
    string? TargetType = null,
    string? TargetId = null,
    AuditOutcome? Outcome = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
