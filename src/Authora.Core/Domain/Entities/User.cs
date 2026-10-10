// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Entities;

/// <summary>
///     Represents a User.
/// </summary>
public sealed class User
{
    /// <summary>
    ///     Id of the User.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>
    ///     Email of the User.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    ///     First Name of the User.
    /// </summary>
    public required string FirstName { get; set; }

    /// <summary>
    ///     Last Name of the User.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>
    ///     The State of the User.
    /// </summary>
    public required UserState State { get; set; }
}

/// <summary>
///     Defines the State of a User.
/// </summary>
public enum UserState
{
    /// <summary>
    ///     The User is Active.
    /// </summary>
    Activ = 0,

    /// <summary>
    ///     The User is Inactive (when the user didn't log in for a defined while).
    /// </summary>
    Inactiv = 1,

    /// <summary>
    ///     The User got deleted.
    /// </summary>
    Deleted = 2
}
