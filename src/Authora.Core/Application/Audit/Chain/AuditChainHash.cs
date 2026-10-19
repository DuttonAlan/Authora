// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Audit.Chain;

/// <summary>
///     Computes the hashes of the audit chain.
/// </summary>
/// <remarks>
///     The hash is SHA-256 over a JSON array of the Sequence, the previous hash and every field of the event, so field
///     boundaries are unambiguous. <see cref="AuditEvent.OccurredAt"/> is written with microseconds, the precision
///     PostgreSQL stores, so an event hashes the same before and after it was saved. Changing the format breaks every
///     existing chain.
/// </remarks>
public static class AuditChainHash
{
    /// <summary>
    ///     The previous hash of the first link.
    /// </summary>
    public static readonly string Genesis = new('0', AuditChainLink.HashLength);

    /// <summary>
    ///     Computes the hash of a link.
    /// </summary>
    /// <param name="sequence">The Sequence of the link.</param>
    /// <param name="previousHash">The hash of the previous link, or <see cref="Genesis"/>.</param>
    /// <param name="auditEvent">The sealed event, as stored in the database.</param>
    /// <returns>The hash as lowercase hex.</returns>
    public static string Compute(long sequence, string previousHash, AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(previousHash);
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(sequence);
            writer.WriteStringValue(previousHash);
            writer.WriteStringValue(auditEvent.Id.ToString("D"));
            writer.WriteStringValue(auditEvent.OccurredAt.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture));
            writer.WriteStringValue(auditEvent.Type);
            writer.WriteStringValue(auditEvent.Outcome.ToString());
            WriteNullable(writer, auditEvent.ActorUserId?.ToString("D"));
            WriteNullable(writer, auditEvent.ActorClientId?.ToString("D"));
            WriteNullable(writer, auditEvent.IpAddress);
            WriteNullable(writer, auditEvent.TargetType);
            WriteNullable(writer, auditEvent.TargetId);
            WriteNullable(writer, auditEvent.Details);
            writer.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteNullable(Utf8JsonWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
