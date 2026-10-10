// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Data.Entities;

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
}
