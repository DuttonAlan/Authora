// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Audit;

/// <summary>
///     An audit event to record through <see cref="Abstractions.IAuditLog"/>. The actor (User, Client and IP address)
///     and the time are added by the audit log.
/// </summary>
/// <param name="Type">What happened, see <see cref="AuditEventTypes"/>.</param>
/// <param name="TargetType">The kind of object the event is about, see <see cref="AuditTargetTypes"/>, or null.</param>
/// <param name="TargetId">The Id of the object, or null.</param>
/// <param name="Details">A short description without secrets, or null.</param>
public sealed record AuditEntry(
    string Type,
    string? TargetType = null,
    string? TargetId = null,
    string? Details = null)
{
    /// <summary>
    ///     Whether the action succeeded. Defaults to <see cref="AuditOutcome.Succeeded"/>.
    /// </summary>
    public AuditOutcome Outcome { get; init; } = AuditOutcome.Succeeded;

    /// <summary>
    ///     The Client that acted, if the request is not authenticated with an Access Token, e.g. at the token
    ///     endpoint. Otherwise the Client of the Access Token is used.
    /// </summary>
    public Guid? ClientId { get; init; }

    /// <summary>
    ///     Creates an entry about a User.
    /// </summary>
    /// <param name="type">What happened.</param>
    /// <param name="userId">The Id of the User.</param>
    /// <param name="details">A short description, or null.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ForUser(string type, Guid userId, string? details = null)
        => new(type, AuditTargetTypes.User, userId.ToString(), details);

    /// <summary>
    ///     Creates an entry about a Role.
    /// </summary>
    /// <param name="type">What happened.</param>
    /// <param name="roleId">The Id of the Role.</param>
    /// <param name="details">A short description, or null.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ForRole(string type, Guid roleId, string? details = null)
        => new(type, AuditTargetTypes.Role, roleId.ToString(), details);

    /// <summary>
    ///     Creates an entry about a Group.
    /// </summary>
    /// <param name="type">What happened.</param>
    /// <param name="groupId">The Id of the Group.</param>
    /// <param name="details">A short description, or null.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ForGroup(string type, Guid groupId, string? details = null)
        => new(type, AuditTargetTypes.Group, groupId.ToString(), details);

    /// <summary>
    ///     Creates an entry about a Scope.
    /// </summary>
    /// <param name="type">What happened.</param>
    /// <param name="scopeId">The Id of the Scope.</param>
    /// <param name="details">A short description, or null.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ForScope(string type, Guid scopeId, string? details = null)
        => new(type, AuditTargetTypes.Scope, scopeId.ToString(), details);

    /// <summary>
    ///     Creates an entry about a Client.
    /// </summary>
    /// <param name="type">What happened.</param>
    /// <param name="clientId">The Id of the Client.</param>
    /// <param name="details">A short description, or null.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ForClient(string type, Guid clientId, string? details = null)
        => new(type, AuditTargetTypes.Client, clientId.ToString(), details);
}
