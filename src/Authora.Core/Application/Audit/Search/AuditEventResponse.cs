// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Audit.Search;

/// <summary>
///     An audit event.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4b1e2a7c9d01">Id of the event.</param>
/// <param name="OccurredAt" example="2026-10-14T09:30:00Z">When the event occurred (UTC).</param>
/// <param name="Type" example="user.role-assigned">What happened.</param>
/// <param name="Outcome" example="Succeeded">Whether the action succeeded.</param>
/// <param name="ActorUserId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The User who acted, or null.</param>
/// <param name="ActorEmail" example="admin@example.com">The current Email of the User who acted, or null.</param>
/// <param name="ActorClientId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">The Client that acted, or null.</param>
/// <param name="ActorClientName" example="Authora Admin">The current Name of the Client that acted, or null.</param>
/// <param name="IpAddress" example="203.0.113.7">The IP address of the request, or null for events of Authora itself.</param>
/// <param name="TargetType" example="user">The kind of object the event is about, or null.</param>
/// <param name="TargetId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The Id of the object, or null.</param>
/// <param name="Details" example="Role 'support' (0199d1a2-7c3e-7b4a-9f10-2c963f66afa6)">A short description, or null.</param>
public sealed record AuditEventResponse(
    Guid Id,
    DateTime OccurredAt,
    string Type,
    AuditOutcome Outcome,
    Guid? ActorUserId,
    string? ActorEmail,
    Guid? ActorClientId,
    string? ActorClientName,
    string? IpAddress,
    string? TargetType,
    string? TargetId,
    string? Details);
