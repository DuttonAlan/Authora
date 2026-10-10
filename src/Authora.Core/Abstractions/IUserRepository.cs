// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for User operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    ///     Create a new User.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The Id of the created User.</returns>
    public Task<Guid> CreateUser(User user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a User by Id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Users.
    /// </summary>
    /// <returns></returns>
    public Task<IReadOnlyList<User>> GetUsers(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update a User by Id.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task UpdateUser(User user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete a User by Id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task DeleteUser(Guid id, CancellationToken cancellationToken = default);
}
