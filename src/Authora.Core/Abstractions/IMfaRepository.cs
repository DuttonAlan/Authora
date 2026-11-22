// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the multi-factor authentication of Users: their authenticator app and recovery codes.
/// </summary>
/// <remarks>
///     Changes are staged in the request's DbContext and saved by <see cref="SaveChangesAsync"/> (or the
///     <c>SaveChangesAsync</c> of another repository), together with the staged audit event. Only the uses of a code
///     are saved at once, in a single atomic update each.
/// </remarks>
public interface IMfaRepository
{
    /// <summary>
    ///     Starts the transaction for changing the multi-factor authentication of a User, and locks it until the
    ///     transaction ends, so that a setup, its confirmation, new recovery codes, a removal and a reset of the same
    ///     User run one after the other. Load the authenticator and the codes after this call. Joins the transaction of
    ///     the request if there is one, e.g. that of <see cref="IUserRepository.BeginPrivilegeChangeAsync"/>; the lock
    ///     is then held until that transaction ends.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The transaction. Committing a joined transaction is left to its owner.</returns>
    public Task<ITransaction> BeginMfaChangeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the authenticator app of a User, confirmed or not, tracked so it can be changed or removed.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The authenticator, or null if the User has none.</returns>
    public Task<TotpAuthenticator?> GetTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the confirmed authenticator app of a User without tracking it, to verify a code.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The authenticator, or null if the User has none or did not confirm it.</returns>
    public Task<TotpAuthenticator?> FindConfirmedTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a User has a confirmed authenticator app.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the User must enter a code after the password.</returns>
    public Task<bool> HasConfirmedTotpAuthenticator(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stages a new authenticator app.
    /// </summary>
    /// <param name="authenticator">The authenticator.</param>
    public void AddTotpAuthenticator(TotpAuthenticator authenticator);

    /// <summary>
    ///     Stages the removal of an authenticator app.
    /// </summary>
    /// <param name="authenticator">The tracked authenticator.</param>
    public void RemoveTotpAuthenticator(TotpAuthenticator authenticator);

    /// <summary>
    ///     Records that a code of a confirmed authenticator was accepted, in a single atomic update that only succeeds
    ///     if no code of this or a later time step was accepted before. Of two concurrent uses of one code, only one
    ///     succeeds.
    /// </summary>
    /// <param name="authenticatorId">Id of the authenticator.</param>
    /// <param name="timeStep">The time step of the code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the use was recorded; <see langword="false"/> if the time step was already used.</returns>
    public Task<bool> TryRecordTotpUse(Guid authenticatorId, long timeStep, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the recovery codes of a User, used or not, tracked so they can be removed.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The recovery codes, empty if the User has none.</returns>
    public Task<IReadOnlyList<RecoveryCode>> GetRecoveryCodes(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the recovery codes of a User that were not used yet.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of unused codes.</returns>
    public Task<int> CountUnusedRecoveryCodes(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stages new recovery codes.
    /// </summary>
    /// <param name="codes">The codes.</param>
    public void AddRecoveryCodes(IEnumerable<RecoveryCode> codes);

    /// <summary>
    ///     Stages the removal of recovery codes.
    /// </summary>
    /// <param name="codes">The tracked codes.</param>
    public void RemoveRecoveryCodes(IEnumerable<RecoveryCode> codes);

    /// <summary>
    ///     Marks an unused recovery code of a User as used, in a single atomic update, so of two concurrent uses only
    ///     one succeeds.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="codeHash">Hash of the normalized code.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if an unused code of the User matched and is now used.</returns>
    public Task<bool> TryUseRecoveryCode(Guid userId, SecretHash codeHash, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all staged changes.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    /// <exception cref="Application.Exceptions.ConflictException">
    ///     Thrown when another request changed the same authenticator or recovery codes at the same time, e.g. confirmed
    ///     the setup or replaced the codes.
    /// </exception>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
