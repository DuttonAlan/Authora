// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for pending sign-ins (<see cref="SignInChallenge"/>).
/// </summary>
public interface ISignInChallengeRepository
{
    /// <summary>
    ///     Create a new pending sign-in. Also saves all other tracked changes, e.g. to the User and the staged audit event.
    /// </summary>
    /// <param name="challenge">The pending sign-in to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once it is saved.</returns>
    public Task CreateChallenge(SignInChallenge challenge, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a pending sign-in by the hash of its token, without tracking it.
    /// </summary>
    /// <param name="tokenHash">The hash of the token in the cookie.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The pending sign-in, or null if no pending sign-in has this token.</returns>
    public Task<SignInChallenge?> GetChallengeByTokenHash(SecretHash tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a pending sign-in as completed in a single atomic update, but only if it was not completed yet and has
    ///     not expired.
    /// </summary>
    /// <param name="id">Id of the pending sign-in.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if this call completed it; false if another request did, or it expired.</returns>
    public Task<bool> TryConsume(Guid id, DateTime now, CancellationToken cancellationToken = default);
}
