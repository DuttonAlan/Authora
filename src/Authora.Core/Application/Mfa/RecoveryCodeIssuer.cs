// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Mfa;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Replaces the recovery codes of a User with a new set.
/// </summary>
internal static class RecoveryCodeIssuer
{
    /// <summary>
    ///     Stages the removal of the current codes of the User and a new set of
    ///     <see cref="RecoveryCodeFormat.CountPerSet"/> codes, of which only the hashes are stored.
    /// </summary>
    /// <param name="mfaRepository">The repository to stage the change in.</param>
    /// <param name="secretHasher">The hasher of the codes.</param>
    /// <param name="userId">Id of the User.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The new codes in plain text, to show the User once.</returns>
    public static async Task<IReadOnlyList<string>> ReplaceAsync(
        IMfaRepository mfaRepository,
        ISecretHasher secretHasher,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        mfaRepository.RemoveRecoveryCodes(await mfaRepository.GetRecoveryCodes(userId, cancellationToken));

        var codes = Enumerable.Range(0, RecoveryCodeFormat.CountPerSet).Select(_ => RecoveryCodeFormat.Generate()).ToList();

        mfaRepository.AddRecoveryCodes(codes.Select(code =>
        {
            // Hashed in the form an entered code is normalized to.
            RecoveryCodeFormat.TryNormalize(code, out var normalized);

            return RecoveryCode.Create(userId, secretHasher.Hash(normalized!), now);
        }));

        return codes;
    }
}
