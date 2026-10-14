// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Audit;

namespace Authora.Core.Abstractions;

/// <summary>
///     Provides the actor of the current request for audit events.
/// </summary>
public interface IAuditActorAccessor
{
    /// <summary>
    ///     The actor of the current request, or <see cref="AuditActor.System"/> outside of a request.
    /// </summary>
    public AuditActor Current { get; }
}
