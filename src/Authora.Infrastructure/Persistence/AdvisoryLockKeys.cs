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
    ///     Serializes changes that can remove administrator access, so the last-administrator check stays valid
    ///     until the change is committed.
    /// </summary>
    public const long Administrators = 0x4144_4D49_4E53; // "ADMINS"

    /// <summary>
    ///     Serializes the sealing of audit events, so the Sequence of the chain has no gaps or forks.
    /// </summary>
    public const long AuditChain = 0x4155_4443_4841_494E; // "AUDCHAIN"
}
