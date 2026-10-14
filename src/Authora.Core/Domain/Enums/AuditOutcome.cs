// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Whether the action of an <see cref="Entities.AuditEvent"/> succeeded.
/// </summary>
public enum AuditOutcome
{
    /// <summary>
    ///     The action was carried out.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    ///     The action was attempted but rejected, e.g. a sign-in with a wrong password.
    /// </summary>
    Failed = 1
}
