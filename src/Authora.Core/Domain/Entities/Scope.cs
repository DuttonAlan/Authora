// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Scope a Client can request, which limits what the Client may do on behalf of a User.
/// </summary>
/// <remarks>
///     Scopes are requested via the <c>scope</c> parameter and shown to the User on the consent screen.
///     They are not the same as <see cref="Permission"/>s, which define what a User may do.
/// </remarks>
public sealed class Scope
{
    /// <summary>
    ///     Id of the Scope.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique Name of the Scope as used in the <c>scope</c> parameter, e.g. <c>openid</c> or <c>orders.read</c>.
    ///     Must not contain spaces (RFC 6749, Section 3.3).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Human-readable Name of the Scope, shown to the User on the consent screen.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    ///     Optional Description of what access the Scope grants, shown to the User on the consent screen.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Point in time (UTC) when the Scope was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Scope was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
