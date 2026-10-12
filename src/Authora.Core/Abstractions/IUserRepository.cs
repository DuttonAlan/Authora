// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Paging;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for User operations.
/// </summary>
/// <remarks>
///     Users are changed by loading them tracked via <see cref="GetUserById"/>, changing them
///     through their methods and calling <see cref="SaveChangesAsync"/>.
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    ///     Create a new User.
    /// </summary>
    /// <param name="user">The User to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created User.</returns>
    public Task<Guid> CreateUser(User user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a User by Id, including its Roles.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked User, or null if the User does not exist.</returns>
    public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a User by its normalized Email, without its Roles.
    /// </summary>
    /// <param name="normalizedEmail">The normalized Email, see <see cref="User.Normalize(string)"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked User, or null if no User has this Email.</returns>
    public Task<User?> GetUserByNormalizedEmail(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a User by Id, including its Roles and their Permissions. The User is not tracked.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The User, or null if the User does not exist.</returns>
    public Task<User?> GetUserWithPermissionsById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get one page of Users ordered by their Email.
    /// </summary>
    /// <param name="state">Only return Users in this State, or null for all Users that are not deleted.</param>
    /// <param name="search">Only return Users whose Email, First Name or Last Name contains this text (case-insensitive), or null for no filter.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Users on the page and the total number of matching Users.</returns>
    public Task<PagedResponse<User>> GetUsers(
        UserState? state,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a User with the given normalized Email exists, including deleted Users.
    /// </summary>
    /// <param name="normalizedEmail">The normalized Email, see <see cref="User.Normalize(string)"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Email is already used.</returns>
    public Task<bool> EmailExists(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts a transaction that holds the administrator lock until it is committed or disposed. Only one
    ///     such transaction runs at a time, so a check of the remaining administrators stays valid until the change
    ///     is saved and committed.
    /// </summary>
    /// <remarks>
    ///     Load the User after this call, call <see cref="SaveChangesAsync"/> and then
    ///     <see cref="ITransaction.CommitAsync"/>.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction, which holds the lock.</returns>
    public Task<ITransaction> BeginAdministratorChangeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the Users that have the given Role and can sign in, excluding one User.
    /// </summary>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="excludedUserId">Id of the User that is not counted.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of matching Users.</returns>
    public Task<int> CountUsersWithRoleWhoCanSignIn(
        Guid roleId,
        Guid excludedUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a failed sign-in attempt in a single atomic update, so concurrent attempts are all counted.
    ///     Locks the User out once <paramref name="maxFailedAttempts"/> is reached and resets the counter.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="maxFailedAttempts">The number of failed attempts in a row that lock the User out.</param>
    /// <param name="lockoutDuration">How long the User is locked out.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The end of the User's lockout after the update, or null if the User was never locked out.</returns>
    public Task<DateTime?> RecordFailedSignIn(
        Guid id,
        DateTime now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Users.
    /// </summary>
    /// <remarks>
    ///     Fails if another request changed one of the Users since it was loaded. The outdated Users are then no longer
    ///     tracked, so loading them again returns the current values.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    /// <exception cref="Application.Exceptions.ConflictException">Thrown when another request changed a User meanwhile.</exception>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
