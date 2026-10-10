// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Role, a reusable set of <see cref="Permission"/>s that can be assigned to Users.
/// </summary>
public sealed class Role
{
    /// <summary>
    ///     Name of the built-in System Role that has all Permissions.
    /// </summary>
    public const string AdministratorName = "admin";

    private readonly List<Permission> _permissions = [];

    /// <summary>
    ///     Id of the Role.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique technical Name of the Role, e.g. <c>admin</c>.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Human-readable Name of the Role, e.g. shown in the admin interface.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    ///     Optional Description of what the Role is used for.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Whether the Role is a built-in System Role. System Roles are protected against deletion.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    ///     Permissions granted to every User that has this Role.
    /// </summary>
    public IReadOnlyCollection<Permission> Permissions => _permissions;

    /// <summary>
    ///     Point in time (UTC) when the Role was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Role was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Grants a Permission to the Role. Does nothing if the Permission is already granted.
    /// </summary>
    /// <remarks>
    ///     <see cref="Permissions"/> must be loaded, otherwise the duplicate check cannot work.
    /// </remarks>
    /// <param name="permission">The Permission to grant.</param>
    public void GrantPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        if (!_permissions.Any(x => x.Id == permission.Id))
        {
            _permissions.Add(permission);
        }
    }

    /// <summary>
    ///     Revokes a Permission from the Role. Does nothing if the Permission is not granted.
    /// </summary>
    /// <param name="permissionId">The Id of the Permission to revoke.</param>
    public void RevokePermission(Guid permissionId)
    {
        _permissions.RemoveAll(x => x.Id == permissionId);
    }
}
