// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Permission that allows a User to perform a specific action, e.g. <c>users.create</c>.
/// </summary>
/// <remarks>
///     Permissions are granted to Users through <see cref="Role"/>s.
///     They are not the same as <see cref="Scope"/>s, which limit what a Client may do on behalf of a User.
/// </remarks>
public sealed class Permission
{
    /// <summary>
    ///     Id of the Permission.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique technical Name of the Permission, e.g. <c>users.create</c>.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Point in time (UTC) when the Permission was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Permission was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
