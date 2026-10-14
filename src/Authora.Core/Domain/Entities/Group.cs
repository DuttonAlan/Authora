// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Group of Users. Every member has all <see cref="Role"/>s of the Group in addition to their own.
/// </summary>
/// <remarks>
///     A Group can only be created via <see cref="Create(string, string, string?)"/>, so a Group with an invalid Name
///     can never exist. Users join a Group via <see cref="User.JoinGroup(Group)"/>.
/// </remarks>
public sealed class Group
{
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

    private readonly List<Role> _roles = [];

    /// <summary>
    ///     Used by EF Core to materialize a Group from the database.
    /// </summary>
    private Group()
    {
    }

    /// <summary>
    ///     Id of the Group.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique technical Name of the Group, e.g. <c>support-team</c>. Set via <see cref="SetName(string)"/>.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     Human-readable Name of the Group, e.g. shown in the admin interface.
    ///     Set via <see cref="SetDisplayName(string)"/>.
    /// </summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>
    ///     Optional Description of the Group. Set via <see cref="SetDescription(string?)"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    ///     Roles every member of the Group has.
    /// </summary>
    public IReadOnlyCollection<Role> Roles => _roles;

    /// <summary>
    ///     Point in time (UTC) when the Group was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Group was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Creates a new Group without Roles and members.
    /// </summary>
    /// <param name="name">The unique technical Name of the Group, see <see cref="NamePattern"/>.</param>
    /// <param name="displayName">The human-readable Name of the Group.</param>
    /// <param name="description">The optional Description of the Group.</param>
    /// <returns>The created Group.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is empty, too long or the Name has an invalid format.</exception>
    public static Group Create(string name, string displayName, string? description = null)
    {
        var group = new Group();

        group.SetName(name);
        group.SetDisplayName(displayName);
        group.SetDescription(description);

        return group;
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
    ///     Sets the technical Name of the Group.
    /// </summary>
    /// <param name="name">The new Name of the Group, see <see cref="NamePattern"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the Name is empty, too long or has an invalid format.</exception>
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

        Name = trimmed;
    }

    /// <summary>
    ///     Sets the human-readable Name of the Group.
    /// </summary>
    /// <param name="displayName">The new Display Name of the Group.</param>
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
    ///     Sets the Description of the Group. An empty Description removes it.
    /// </summary>
    /// <param name="description">The new Description of the Group, or null or empty to remove it.</param>
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
    ///     Assigns a Role to the Group, so every member has it. Does nothing if the Role is already assigned.
    /// </summary>
    /// <remarks>
    ///     <see cref="Roles"/> must be loaded, otherwise the duplicate check cannot work.
    /// </remarks>
    /// <param name="role">The Role to assign.</param>
    /// <exception cref="InvalidOperationException">Thrown when the Role is the administrator Role.</exception>
    public void AssignRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        // The last-administrator check only counts direct assignments, so the administrator Role must stay direct.
        if (role.IsAdministrator)
        {
            throw new InvalidOperationException("The administrator Role cannot be assigned to a Group.");
        }

        if (!_roles.Any(x => x.Id == role.Id))
        {
            _roles.Add(role);
        }
    }

    /// <summary>
    ///     Removes a Role from the Group. Does nothing if the Role is not assigned.
    /// </summary>
    /// <param name="roleId">The Id of the Role to remove.</param>
    public void RemoveRole(Guid roleId)
    {
        _roles.RemoveAll(x => x.Id == roleId);
    }
}
