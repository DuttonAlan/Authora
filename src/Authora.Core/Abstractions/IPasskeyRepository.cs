// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for the passkeys of Users and the challenges of their WebAuthn ceremonies.
/// </summary>
/// <remarks>
///     Changes to passkeys are staged in the request's DbContext and saved by <see cref="SaveChangesAsync"/> (or the
///     <c>SaveChangesAsync</c> of another repository), together with the staged audit event. Changes of a User's
///     passkeys run under <see cref="IMfaRepository.BeginMfaChangeAsync"/>. Challenges and the uses of a passkey are
///     saved at once, the uses and consumptions in a single atomic update each.
/// </remarks>
public interface IPasskeyRepository
{
    /// <summary>
    ///     Get the passkeys of a User, tracked so they can be changed or removed.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The passkeys, oldest first; empty if the User has none.</returns>
    public Task<IReadOnlyList<Passkey>> GetPasskeys(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a passkey of a User, tracked so it can be changed or removed.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The passkey, or null if the User has no passkey with this Id.</returns>
    public Task<Passkey?> GetPasskey(Guid userId, Guid passkeyId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a passkey of a User by its credential Id without tracking it, to verify a response.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="credentialId">The credential Id from the response.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The passkey, or null if the User has none with this credential Id.</returns>
    public Task<Passkey?> FindPasskeyByCredentialId(Guid userId, byte[] credentialId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a User has at least one passkey.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the User can and must use a second factor after the password.</returns>
    public Task<bool> HasPasskeys(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether any User registered a credential Id.
    /// </summary>
    /// <param name="credentialId">The credential Id.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if it is already registered.</returns>
    public Task<bool> CredentialIdExists(byte[] credentialId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stages a new passkey.
    /// </summary>
    /// <param name="passkey">The passkey.</param>
    public void AddPasskey(Passkey passkey);

    /// <summary>
    ///     Stages the removal of passkeys.
    /// </summary>
    /// <param name="passkeys">The tracked passkeys.</param>
    public void RemovePasskeys(IEnumerable<Passkey> passkeys);

    /// <summary>
    ///     Records that a passkey was used, in a single atomic update that only succeeds if the signature counter
    ///     increased since it was loaded, or both are 0 (an authenticator that does not count).
    /// </summary>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="signCount">The signature counter of the response.</param>
    /// <param name="isBackedUp">Whether the passkey is backed up now.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the use was recorded; <see langword="false"/> if the passkey is gone or the counter did not increase.</returns>
    public Task<bool> TryRecordUse(Guid passkeyId, uint signCount, bool isBackedUp, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves a new challenge at once.
    /// </summary>
    /// <param name="challenge">The challenge.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the challenge is saved.</returns>
    public Task CreateChallenge(PasskeyChallenge challenge, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Consumes a challenge in a single atomic update, so of two concurrent responses to it only one succeeds.
    /// </summary>
    /// <param name="challengeHash">Hash of the challenge the response signed.</param>
    /// <param name="userId">Id of the User the response is for.</param>
    /// <param name="ceremony">The ceremony the response is for.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if an unused, unexpired challenge of the User for the ceremony matched and is now used.</returns>
    public Task<bool> TryConsumeChallenge(
        SecretHash challengeHash,
        Guid userId,
        PasskeyCeremony ceremony,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all staged changes.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    /// <exception cref="Application.Exceptions.ConflictException">
    ///     Thrown when another request registered the same credential Id or changed the passkeys at the same time.
    /// </exception>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
