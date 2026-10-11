// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a Scope a Client can request, which limits what the Client may do on behalf of a User.
/// </summary>
/// <remarks>
///     Scopes are requested via the <c>scope</c> parameter and shown to the User on the consent screen.
///     They are not the same as <see cref="Permission"/>s, which define what a User may do.
///     A Scope can only be created via <see cref="Create(string, string, string?)"/>,
///     so a Scope with an invalid Name can never exist.
/// </remarks>
public sealed class Scope
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
    ///     Regular expression the Name must match: the characters allowed in a scope token
    ///     by RFC 6749, Section 3.3 (printable ASCII without space, <c>"</c> and <c>\</c>).
    /// </summary>
    public const string NamePattern = @"^[\x21\x23-\x5B\x5D-\x7E]+$";

    /// <summary>
    ///     Used by EF Core to materialize a Scope from the database.
    /// </summary>
    private Scope()
    {
    }

    /// <summary>
    ///     Id of the Scope.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Unique Name of the Scope as used in the <c>scope</c> parameter, e.g. <c>openid</c> or <c>orders.read</c>.
    ///     Case-sensitive and must not contain spaces (RFC 6749, Section 3.3).
    ///     Cannot be changed after creation, because Clients send it in their requests.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    ///     Human-readable Name of the Scope, shown to the User on the consent screen.
    ///     Set via <see cref="SetDisplayName(string)"/>.
    /// </summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>
    ///     Optional Description of what access the Scope grants, shown to the User on the consent screen.
    ///     Set via <see cref="SetDescription(string?)"/>.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    ///     Whether the Scope is a built-in System Scope, e.g. <c>openid</c>. System Scopes cannot be deleted.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    ///     Point in time (UTC) when the Scope was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the Scope was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Creates a new Scope.
    /// </summary>
    /// <remarks>
    ///     Always creates a regular Scope. System Scopes, e.g. <c>openid</c>, are only created by seeding.
    /// </remarks>
    /// <param name="name">The unique Name of the Scope, see <see cref="NamePattern"/>.</param>
    /// <param name="displayName">The human-readable Name of the Scope.</param>
    /// <param name="description">The optional Description of the Scope.</param>
    /// <returns>The created Scope.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is empty, too long or the Name has an invalid format.</exception>
    public static Scope Create(string name, string displayName, string? description = null)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();

        if (!IsValidName(trimmed))
        {
            throw new DomainValidationException(
                $"Name must be at most {NameMaxLength} characters and only contain printable ASCII characters except space, '\"' and '\\'.",
                nameof(name));
        }

        var scope = new Scope
        {
            Name = trimmed
        };

        scope.SetDisplayName(displayName);
        scope.SetDescription(description);

        return scope;
    }

    /// <summary>
    ///     Checks whether a Name is a valid scope token (RFC 6749, Section 3.3):
    ///     not empty, at most <see cref="NameMaxLength"/> characters and only <c>%x21 / %x23-5B / %x5D-7E</c>.
    /// </summary>
    /// <param name="name">The Name to check.</param>
    /// <returns>True if the Name is valid.</returns>
    public static bool IsValidName(string? name)
    {
        return !string.IsNullOrEmpty(name)
            && name.Length <= NameMaxLength
            && name.All(c => c is '\x21' or (>= '\x23' and <= '\x5B') or (>= '\x5D' and <= '\x7E'));
    }

    /// <summary>
    ///     Sets the human-readable Name of the Scope.
    /// </summary>
    /// <param name="displayName">The new Display Name of the Scope.</param>
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
    ///     Sets the Description of the Scope. An empty Description removes it.
    /// </summary>
    /// <param name="description">The new Description of the Scope, or null or empty to remove it.</param>
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
}
