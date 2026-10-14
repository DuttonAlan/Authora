// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     A security-relevant event, e.g. a sign-in or a change through the admin API (US-034).
/// </summary>
/// <remarks>
///     Audit events are only ever added, never changed, and are deleted only by the cleanup after their retention.
///     They never contain passwords, tokens, codes or Secrets, only Ids and Names.
/// </remarks>
public sealed class AuditEvent
{
    /// <summary>
    ///     Maximum length of the Type.
    /// </summary>
    public const int TypeMaxLength = 100;

    /// <summary>
    ///     Maximum length of the Target Type.
    /// </summary>
    public const int TargetTypeMaxLength = 50;

    /// <summary>
    ///     Maximum length of the Target Id.
    /// </summary>
    public const int TargetIdMaxLength = 200;

    /// <summary>
    ///     Maximum length of the Details. Longer Details are cut off.
    /// </summary>
    public const int DetailsMaxLength = 1000;

    /// <summary>
    ///     Maximum length of the IP address (the longest textual IPv6 address).
    /// </summary>
    public const int IpAddressMaxLength = 45;

    /// <summary>
    ///     Used by EF Core to materialize an AuditEvent from the database.
    /// </summary>
    private AuditEvent()
    {
    }

    /// <summary>
    ///     Id of the event. Version 7, so it sorts by creation time.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Point in time (UTC) when the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    ///     What happened, e.g. <c>user.created</c>. See <c>AuditEventTypes</c>.
    /// </summary>
    public string Type { get; private set; } = null!;

    /// <summary>
    ///     Whether the action succeeded.
    /// </summary>
    public AuditOutcome Outcome { get; private set; }

    /// <summary>
    ///     Id of the User who acted, or <see langword="null"/> if nobody was signed in, e.g. for a failed sign-in or
    ///     an event of Authora itself.
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>
    ///     Id of the Client the User acted through, or that acted itself, or <see langword="null"/>.
    /// </summary>
    public Guid? ActorClientId { get; private set; }

    /// <summary>
    ///     IP address of the request, or <see langword="null"/> for events of Authora itself.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    ///     The kind of object the event is about, e.g. <c>user</c>, or <see langword="null"/>.
    /// </summary>
    public string? TargetType { get; private set; }

    /// <summary>
    ///     Id of the object the event is about, e.g. the User Id or the <c>kid</c> of a key, or <see langword="null"/>.
    /// </summary>
    public string? TargetId { get; private set; }

    /// <summary>
    ///     A short human-readable description, e.g. the Name of an assigned Role, or <see langword="null"/>.
    ///     Never contains secrets.
    /// </summary>
    public string? Details { get; private set; }

    /// <summary>
    ///     Creates an event.
    /// </summary>
    /// <param name="type">What happened, e.g. <c>user.created</c>.</param>
    /// <param name="outcome">Whether the action succeeded.</param>
    /// <param name="occurredAt">When the event occurred (UTC).</param>
    /// <param name="actorUserId">The User who acted, or null.</param>
    /// <param name="actorClientId">The Client that acted, or null.</param>
    /// <param name="ipAddress">The IP address of the request, or null. Cut off at <see cref="IpAddressMaxLength"/>.</param>
    /// <param name="targetType">The kind of object the event is about, or null.</param>
    /// <param name="targetId">The Id of the object, or null.</param>
    /// <param name="details">A short description, or null. Cut off at <see cref="DetailsMaxLength"/>.</param>
    /// <returns>The created event.</returns>
    /// <exception cref="DomainValidationException">Thrown when the Type is empty or a value is too long.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="occurredAt"/> is not in UTC.</exception>
    public static AuditEvent Create(
        string type,
        AuditOutcome outcome,
        DateTime occurredAt,
        Guid? actorUserId,
        Guid? actorClientId,
        string? ipAddress,
        string? targetType,
        string? targetId,
        string? details)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(type);
        UtcGuard.EnsureUtc(occurredAt);

        if (type.Length > TypeMaxLength)
        {
            throw new DomainValidationException($"Type must not be longer than {TypeMaxLength} characters.", nameof(type));
        }

        if (targetType?.Length > TargetTypeMaxLength)
        {
            throw new DomainValidationException($"Target Type must not be longer than {TargetTypeMaxLength} characters.", nameof(targetType));
        }

        if (targetId?.Length > TargetIdMaxLength)
        {
            throw new DomainValidationException($"Target Id must not be longer than {TargetIdMaxLength} characters.", nameof(targetId));
        }

        return new AuditEvent
        {
            Type = type,
            Outcome = outcome,
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorClientId = actorClientId,
            IpAddress = Truncate(ipAddress, IpAddressMaxLength),
            TargetType = targetType,
            TargetId = targetId,
            // An event must never be lost because of a long Name in its description.
            Details = Truncate(details, DetailsMaxLength)
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
