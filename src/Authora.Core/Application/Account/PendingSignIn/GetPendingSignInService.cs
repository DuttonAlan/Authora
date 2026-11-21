// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.SecondFactors;

namespace Authora.Core.Application.Account.PendingSignIn;

/// <summary>
///     The Service to show a pending sign-in: who is signing in and which second factors they can complete.
/// </summary>
/// <param name="reader">Loads and checks the pending sign-in.</param>
public sealed class GetPendingSignInService(PendingSignInReader reader)
{
    /// <summary>
    ///     Gets the pending sign-in of the browser.
    /// </summary>
    /// <param name="challengeToken">The plain-text token from the cookie.</param>
    /// <param name="currentSessionId">Id of the Session of the browser, or null if it has none.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The pending sign-in, or null if it cannot be completed (anymore).</returns>
    public async Task<PendingSignInResponse?> ExecuteAsync(
        string? challengeToken,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var pending = await reader.ReadAsync(challengeToken, currentSessionId, cancellationToken);

        if (pending is null)
        {
            return null;
        }

        return new PendingSignInResponse(
            pending.User.Email,
            pending.Session is not null,
            [.. pending.Factors.Select(factor => new SecondFactorResponse(factor.Method, factor.DisplayName))],
            pending.Challenge.ExpiresAt);
    }
}
