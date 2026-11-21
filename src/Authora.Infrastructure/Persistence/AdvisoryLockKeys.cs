// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Infrastructure.Persistence;

/// <summary>
///     Keys of the PostgreSQL advisory locks (<c>pg_advisory_xact_lock</c>). Each key must be unique, otherwise
///     unrelated operations block each other.
/// </summary>
internal static class AdvisoryLockKeys
{
    /// <summary>
    ///     Serializes seeding across instances.
    /// </summary>
    public const long Seed = 0x4155_5448_4F52_41; // "AUTHORA"

    /// <summary>
    ///     Serializes changes that can remove administrator access and changes that are checked against the
    ///     Permissions of the caller (<c>PrivilegeGuard</c>), so these checks stay valid until the change is committed.
    /// </summary>
    public const long Privileges = 0x5052_4956_494C_4547; // "PRIVILEG"

    /// <summary>
    ///     Serializes the sealing of audit events, so the Sequence of the chain has no gaps or forks.
    /// </summary>
    public const long AuditChain = 0x4155_4443_4841_494E; // "AUDCHAIN"

    /// <summary>
    ///     First key of the two-key locks (<c>pg_advisory_xact_lock(int, int)</c>, a key space of its own) that
    ///     serialize replacing the email tokens of one User and purpose. The second key is derived from both.
    /// </summary>
    public const int EmailTokens = 0x454D_4C54; // "EMLT"

    /// <summary>
    ///     First key of the two-key locks that serialize changes to the Consent of one User for one Client. The second
    ///     key is derived from both Ids.
    /// </summary>
    public const int Consents = 0x434F_4E53; // "CONS"
}
