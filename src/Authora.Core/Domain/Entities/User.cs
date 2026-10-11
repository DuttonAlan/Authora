// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net.Mail;

using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a User, the identity that signs in to Authora.
/// </summary>
/// <remarks>
///     A User can only be created via <see cref="Create(string, string, string)"/>,
///     so a User without a valid Email can never exist.
/// </remarks>
public sealed class User
{
    /// <summary>
    ///     Maximum length of an Email (RFC 5321).
    /// </summary>
    public const int EmailMaxLength = 254;

    /// <summary>
    ///     Maximum length of the First Name and of the Last Name.
    /// </summary>
    public const int NameMaxLength = 50;

    private readonly List<Role> _roles = [];

    /// <summary>
    ///     Used by EF Core to materialize a User from the database.
    /// </summary>
    private User()
    {
    }

    /// <summary>
    ///     Id of the User. Also used as the <c>sub</c> claim in issued tokens.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Email of the User as entered. Used for display and sending emails.
    ///     Set via <see cref="SetEmail(string)"/>.
    /// </summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    ///     Normalized Email of the User (trimmed and lowercase). Used for uniqueness checks and lookups.
    ///     Set via <see cref="SetEmail(string)"/>.
    /// </summary>
    public string NormalizedEmail { get; private set; } = null!;

    /// <summary>
    ///     First Name of the User. Set via <see cref="SetFirstName(string)"/>.
    /// </summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>
    ///     Last Name of the User. Set via <see cref="SetLastName(string)"/>.
    /// </summary>
    public string LastName { get; private set; } = null!;

    /// <summary>
    ///     Hash of the password of the User, or <see langword="null"/> if the User has not set a password yet.
    ///     A User without a password cannot sign in. The plain-text password is never stored.
    ///     Set via <see cref="SetPasswordHash(string)"/>.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    ///     Number of failed login attempts in a row. Reset after a successful login or via <see cref="Unlock"/>.
    /// </summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>
    ///     Point in time (UTC) until which the User is locked out after too many failed login attempts,
    ///     or <see langword="null"/> if the User is not locked out. Reset via <see cref="Unlock"/>.
    /// </summary>
    public DateTime? LockoutEndAt { get; private set; }

    /// <summary>
    ///     State of the User. Only <see cref="UserState.Active"/> and <see cref="UserState.Inactive"/> Users can sign in.
    ///     Changed via <see cref="Activate"/>, <see cref="Deactivate"/> and <see cref="MarkDeleted"/>.
    /// </summary>
    public UserState State { get; private set; } = UserState.Active;

    /// <summary>
    ///     Point in time (UTC) when the User was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Point in time (UTC) when the User was last modified, or <see langword="null"/> if it was never modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    ///     Point in time (UTC) of the last successful login, or <see langword="null"/> if the User never logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    ///     Random value that changes on security-relevant events. Sessions and Refresh Tokens that were
    ///     issued with a different value are no longer valid.
    /// </summary>
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();

    /// <summary>
    ///     Roles assigned to the User. The User has all Permissions of these Roles.
    /// </summary>
    public IReadOnlyCollection<Role> Roles => _roles;

    /// <summary>
    ///     Whether the User may sign in and use tokens: the User is <see cref="UserState.Active"/> or
    ///     <see cref="UserState.Inactive"/> and has a password. Does not check the lockout, see <see cref="IsLockedOut(DateTime)"/>.
    /// </summary>
    public bool CanSignIn => (State is UserState.Active or UserState.Inactive) && PasswordHash is not null;

    /// <summary>
    ///     Creates a new <see cref="UserState.Active"/> User without a password.
    /// </summary>
    /// <remarks>
    ///     The User cannot sign in until a password is set via <see cref="SetPasswordHash(string)"/>,
    ///     e.g. through the password reset flow.
    /// </remarks>
    /// <param name="email">The Email of the User.</param>
    /// <param name="firstName">The First Name of the User.</param>
    /// <param name="lastName">The Last Name of the User.</param>
    /// <returns>The created User.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is empty or too long, or the Email is not valid.</exception>
    public static User Create(string email, string firstName, string lastName)
    {
        var user = new User();

        user.SetFirstName(firstName);
        user.SetLastName(lastName);
        user.SetEmail(email);

        return user;
    }

    /// <summary>
    ///     Sets the First Name of the User. Leading and trailing whitespace is removed.
    /// </summary>
    /// <param name="firstName">The new First Name of the User.</param>
    /// <exception cref="ArgumentException">Thrown when the First Name is empty, only whitespace or too long.</exception>
    public void SetFirstName(string firstName)
    {
        FirstName = ValidateName(firstName, nameof(firstName));
    }

    /// <summary>
    ///     Sets the Last Name of the User. Leading and trailing whitespace is removed.
    /// </summary>
    /// <param name="lastName">The new Last Name of the User.</param>
    /// <exception cref="ArgumentException">Thrown when the Last Name is empty, only whitespace or too long.</exception>
    public void SetLastName(string lastName)
    {
        LastName = ValidateName(lastName, nameof(lastName));
    }

    /// <summary>
    ///     Sets a new password hash and rotates the <see cref="SecurityStamp"/>,
    ///     which invalidates all existing Sessions and Refresh Tokens.
    /// </summary>
    /// <param name="passwordHash">The hash of the new password. The plain-text password must never be passed to the domain.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="passwordHash"/> is empty.</exception>
    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        RotateSecurityStamp();
    }

    /// <summary>
    ///     Replaces the password hash with a new hash of the same password, e.g. after the hashing parameters were
    ///     increased. Unlike <see cref="SetPasswordHash(string)"/>, it does not rotate the <see cref="SecurityStamp"/>,
    ///     because the password did not change.
    /// </summary>
    /// <param name="passwordHash">The new hash of the current password.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="passwordHash"/> is empty.</exception>
    public void RehashPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }

    /// <summary>
    ///     Checks whether the User is locked out after too many failed sign-in attempts.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns><see langword="true"/> if the lockout has not ended yet.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public bool IsLockedOut(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        return LockoutEndAt > now;
    }

    /// <summary>
    ///     Records a successful sign-in: resets the failed attempts, sets <see cref="LastLoginAt"/> and makes an
    ///     <see cref="UserState.Inactive"/> User <see cref="UserState.Active"/> again.
    /// </summary>
    /// <param name="now">The current point in time (UTC).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="now"/> is not in UTC.</exception>
    public void RecordSuccessfulSignIn(DateTime now)
    {
        UtcGuard.EnsureUtc(now);

        FailedLoginCount = 0;
        LockoutEndAt = null;
        LastLoginAt = now;

        if (State == UserState.Inactive)
        {
            State = UserState.Active;
        }
    }

    /// <summary>
    ///     Assigns a Role to the User. Does nothing if the Role is already assigned.
    /// </summary>
    /// <remarks>
    ///     <see cref="Roles"/> must be loaded, otherwise the duplicate check cannot work.
    /// </remarks>
    /// <param name="role">The Role to assign.</param>
    public void AssignRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (!_roles.Any(x => x.Id == role.Id))
        {
            _roles.Add(role);
        }
    }

    /// <summary>
    ///     Removes a Role from the User. Does nothing if the Role is not assigned.
    /// </summary>
    /// <param name="roleId">The Id of the Role to remove.</param>
    public void RemoveRole(Guid roleId)
    {
        _roles.RemoveAll(x => x.Id == roleId);
    }

    /// <summary>
    ///     Generates a new <see cref="SecurityStamp"/>, which invalidates all existing Sessions and Refresh Tokens.
    ///     Call it e.g. on password change, deactivation or "sign out everywhere".
    /// </summary>
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid();
    }

    /// <summary>
    ///     Makes the User <see cref="UserState.Active"/>, so the User can sign in again.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the User is deleted, which is final.</exception>
    public void Activate()
    {
        EnsureNotDeleted();

        State = UserState.Active;
    }

    /// <summary>
    ///     Makes the User <see cref="UserState.Deactivated"/>, so the User can no longer sign in,
    ///     and rotates the <see cref="SecurityStamp"/>, which invalidates all existing Sessions and Refresh Tokens.
    /// </summary>
    /// <remarks>
    ///     Does not check whether the User is the last administrator. Callers must check this first.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the User is deleted, which is final.</exception>
    public void Deactivate()
    {
        EnsureNotDeleted();

        State = UserState.Deactivated;
        RotateSecurityStamp();
    }

    /// <summary>
    ///     Makes the User <see cref="UserState.Deleted"/>, which is final, and rotates the <see cref="SecurityStamp"/>,
    ///     which invalidates all existing Sessions and Refresh Tokens. The record is kept for history.
    /// </summary>
    /// <remarks>
    ///     Does not check whether the User is the last administrator. Callers must check this first.
    /// </remarks>
    public void MarkDeleted()
    {
        State = UserState.Deleted;
        RotateSecurityStamp();
    }

    /// <summary>
    ///     Resets the failed login attempts and ends a lockout, so the User can sign in again right away.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the User is deleted, which is final.</exception>
    public void Unlock()
    {
        EnsureNotDeleted();

        FailedLoginCount = 0;
        LockoutEndAt = null;
    }

    // Services check this first and return 409. This is the last line of defense if a new code path forgets it.
    private void EnsureNotDeleted()
    {
        if (State == UserState.Deleted)
        {
            throw new InvalidOperationException("A deleted User cannot be changed.");
        }
    }

    /// <summary>
    ///     Sets the <see cref="Email"/> and the <see cref="NormalizedEmail"/> of the User.
    /// </summary>
    /// <param name="email">The new Email of the User.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="email"/> is empty, too long or not a valid Email.</exception>
    public void SetEmail(string email)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(email);

        var trimmed = email.Trim();

        if (trimmed.Length > EmailMaxLength)
        {
            throw new DomainValidationException($"Email must not be longer than {EmailMaxLength} characters.", nameof(email));
        }

        // MailAddress also accepts "Name <a@b.c>", so the parsed address must equal the input.
        if (!MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
        {
            throw new DomainValidationException("Email is not valid.", nameof(email));
        }

        Email = trimmed;
        NormalizedEmail = Normalize(trimmed);
    }

    /// <summary>
    ///     Normalizes an Email for uniqueness checks and lookups by trimming it and converting it to lowercase.
    /// </summary>
    /// <param name="email">The Email to normalize.</param>
    /// <returns>The normalized Email.</returns>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string ValidateName(string name, string paramName)
    {
        DomainValidationException.ThrowIfNullOrWhiteSpace(name, paramName);

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException($"Name must not be longer than {NameMaxLength} characters.", paramName);
        }

        return trimmed;
    }
}
