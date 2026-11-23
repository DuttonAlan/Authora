// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.ValueObjects;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     Creates the challenges of WebAuthn ceremonies and consumes them once a response arrives.
/// </summary>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to store and consume the challenges.</param>
/// <param name="secretHasher">The hasher for the stored challenges.</param>
/// <param name="timeProvider">The TimeProvider for the expiry.</param>
public sealed class PasskeyChallengeIssuer(
    IPasskeyRepository passkeyRepository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider)
{
    /// <summary>
    ///     The WebAuthn user handle of a User: the bytes of their Id, which contain nothing personal.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <returns>The user handle.</returns>
    public static byte[] UserHandle(Guid userId) => userId.ToByteArray();

    /// <summary>
    ///     Creates and stores a new random challenge.
    /// </summary>
    /// <param name="userId">Id of the User the ceremony is for.</param>
    /// <param name="ceremony">The ceremony.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The challenge to put into the options.</returns>
    public async Task<byte[]> IssueAsync(Guid userId, PasskeyCeremony ceremony, CancellationToken cancellationToken = default)
    {
        var challenge = RandomNumberGenerator.GetBytes(PasskeyChallenge.Length);

        await passkeyRepository.CreateChallenge(
            PasskeyChallenge.Create(Hash(challenge), userId, ceremony, timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);

        return challenge;
    }

    /// <summary>
    ///     Consumes the challenge a response signed, whatever the outcome of verifying the response: it is accepted at
    ///     most once.
    /// </summary>
    /// <param name="challenge">The challenge from the response.</param>
    /// <param name="userId">Id of the User the response is for.</param>
    /// <param name="ceremony">The ceremony the response is for.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the challenge was issued for the User and ceremony, unused and not expired.</returns>
    public Task<bool> TryConsumeAsync(byte[] challenge, Guid userId, PasskeyCeremony ceremony, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return passkeyRepository.TryConsumeChallenge(
            Hash(challenge),
            userId,
            ceremony,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    private SecretHash Hash(byte[] challenge) => secretHasher.Hash(Base64Url.EncodeToString(challenge));
}
