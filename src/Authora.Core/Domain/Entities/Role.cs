// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Role, a reusable set of <see cref="Permission"/>s that can be assigned to Users.
/// </summary>
/// <remarks>
///     A Role can only be created via <see cref="Create(string, string, string?)"/>,
///     so a Role with an invalid Name can never exist.
/// </remarks>
public sealed class Role
{
    /// <summary>
    ///     Name of the built-in System Role that has all Permissions.
    /// </summary>
    public const string AdministratorName = "admin";

    /// <summary>
    ///     Maximum length of the Name.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    ///     Maximum length of the Display Name.
    /// </summary>
    public const int DisplayNameMaxLength = 100;

    /// <summary>
    ///     Maximum length of the Description.
    /// </summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>
    ///     Regular expression the Name must match: lowercase letters, digits, dots, underscores and hyphens.
    /// </summary>
    public const string NamePattern = "^[a-z0-9._-]+$";

    private readonly List<Permission> _permissions = [];

    /// <summary>
    ///     Used by EF Core to materialize a Role from the database.
    /// </summary>
    private Role()
    {
    }

    /// <summary>
    ///     Id of the Role.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique technical Name of the Role, e.g. <c>admin</c>. Set via <see cref="SetName(string)"/>.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     Human-readable Name of the Role, e.g. shown in the admin interface.
    ///     Set via <see cref="SetDisplayName(string)"/>.
    /// </summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>
    ///     Optional Description of what the Role is used for. Set via <see cref="SetDescription(string?)"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    ///     Whether the Role is a built-in System Role. System Roles cannot be renamed or deleted.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    ///     Whether the Role is the built-in administrator Role, which always has all Permissions.
    /// </summary>
    public bool IsAdministrator => IsSystem && Name == AdministratorName;

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
    ///     Creates a new Role without Permissions.
    /// </summary>
    /// <remarks>
    ///     Always creates a regular Role. System Roles, e.g. the administrator Role, are only created by seeding,
    ///     because <see cref="IsAdministrator"/> trusts <see cref="IsSystem"/>.
    /// </remarks>
    /// <param name="name">The unique technical Name of the Role, see <see cref="NamePattern"/>.</param>
    /// <param name="displayName">The human-readable Name of the Role.</param>
    /// <param name="description">The optional Description of the Role.</param>
    /// <returns>The created Role.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is empty, too long or the Name has an invalid format.</exception>
    public static Role Create(string name, string displayName, string? description = null)
    {
        var role = new Role();

        role.SetName(name);
        role.SetDisplayName(displayName);
        role.SetDescription(description);

        return role;
    }

    /// <summary>
    ///     Checks whether a Name has a valid format: not empty, at most <see cref="NameMaxLength"/> characters
    ///     and only lowercase letters, digits, dots, underscores and hyphens.
    /// </summary>
    /// <param name="name">The Name to check.</param>
    /// <returns>True if the Name is valid.</returns>
    public static bool IsValidName(string? name)
    {
        return !string.IsNullOrEmpty(name)
            && name.Length <= NameMaxLength
            && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-');
    }

    /// <summary>
    ///     Sets the technical Name of the Role.
    /// </summary>
    /// <param name="name">The new Name of the Role, see <see cref="NamePattern"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the Name is empty, too long or has an invalid format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Role is a System Role and the Name changes.</exception>
    public void SetName(string name)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (!IsValidName(trimmed))
        {
            throw new DomainValidationException(
                $"Name must be at most {NameMaxLength} characters and only contain lowercase letters, digits, '.', '_' and '-'.",
                nameof(name));
        }

        // Code and seeding refer to System Roles by Name, so it must not change once set.
        if (IsSystem && Name is not null && Name != trimmed)
        {
            throw new InvalidOperationException("The Name of a System Role cannot be changed.");
        }

        Name = trimmed;
    }

    /// <summary>
    ///     Sets the human-readable Name of the Role.
    /// </summary>
    /// <param name="displayName">The new Display Name of the Role.</param>
    /// <exception cref="ArgumentException">Thrown when the Display Name is empty or too long.</exception>
    public void SetDisplayName(string displayName)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(displayName);

        var trimmed = displayName.Trim();

        if (trimmed.Length > DisplayNameMaxLength)
        {
            throw new DomainValidationException($"Display Name must not be longer than {DisplayNameMaxLength} characters.", nameof(displayName));
        }

        DisplayName = trimmed;
    }

    /// <summary>
    ///     Sets the Description of the Role. An empty Description removes it.
    /// </summary>
    /// <param name="description">The new Description of the Role, or null or empty to remove it.</param>
    /// <exception cref="ArgumentException">Thrown when the Description is too long.</exception>
    public void SetDescription(string? description)
    {
        var trimmed = description?.Trim();

        if (trimmed?.Length > DescriptionMaxLength)
        {
            throw new DomainValidationException($"Description must not be longer than {DescriptionMaxLength} characters.", nameof(description));
        }

        Description = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

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
    /// <exception cref="InvalidOperationException">Thrown when the Role is the administrator Role, which always has all Permissions.</exception>
    public void RevokePermission(Guid permissionId)
    {
        if (IsAdministrator && _permissions.Any(x => x.Id == permissionId))
        {
            throw new InvalidOperationException("Permissions cannot be revoked from the administrator Role.");
        }

        _permissions.RemoveAll(x => x.Id == permissionId);
    }
}
