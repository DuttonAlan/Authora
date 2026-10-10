// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a User, the identity that signs in to Authora.
/// </summary>
public sealed class User
{
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
    ///     First Name of the User.
    /// </summary>
    public required string FirstName { get; set; }

    /// <summary>
    ///     Last Name of the User.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>
    ///     Hash of the password of the User. The plain-text password is never stored.
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    ///     Number of failed login attempts in a row. Reset after a successful login.
    /// </summary>
    public int FailedLoginCount { get; set; }

    /// <summary>
    ///     Point in time (UTC) until which the User is locked out after too many failed login attempts,
    ///     or <see langword="null"/> if the User is not locked out.
    /// </summary>
    public DateTime? LockoutEndAt { get; set; }

    /// <summary>
    ///     State of the User. Only <see cref="UserState.Active"/> and <see cref="UserState.Inactive"/> Users can sign in.
    /// </summary>
    public required UserState State { get; set; }

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
    ///     Generates a new <see cref="SecurityStamp"/>, which invalidates all existing Sessions and Refresh Tokens.
    ///     Call it e.g. on password change, deactivation or "sign out everywhere".
    /// </summary>
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid();
    }

    /// <summary>
    ///     Sets the <see cref="Email"/> and the <see cref="NormalizedEmail"/> of the User.
    /// </summary>
    /// <param name="email">The new Email of the User.</param>
    public void SetEmail(string email)
    {
        Email = email.Trim();
        NormalizedEmail = Normalize(email);
    }

    /// <summary>
    ///     Normalizes an Email for uniqueness checks and lookups by trimming it and converting it to lowercase.
    /// </summary>
    /// <param name="email">The Email to normalize.</param>
    /// <returns>The normalized Email.</returns>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>
///     Defines the State of a User.
/// </summary>
public enum UserState
{
    /// <summary>
    ///     The User is active and can sign in.
    /// </summary>
    Active = 0,

    /// <summary>
    ///     The User did not sign in for a defined period of time. The User can still sign in,
    ///     which makes the User <see cref="Active"/> again.
    /// </summary>
    Inactive = 1,

    /// <summary>
    ///     The User was deactivated by an administrator and cannot sign in. The data of the User is kept.
    /// </summary>
    Deactivated = 2,

    /// <summary>
    ///     The User was deleted and cannot sign in. The record is only kept for history.
    /// </summary>
    Deleted = 3
}
