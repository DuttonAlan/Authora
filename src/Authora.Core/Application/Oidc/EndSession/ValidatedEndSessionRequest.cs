// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     A logout request that passed all checks.
/// </summary>
/// <param name="Client">
///     The Client that sent the request, from <c>client_id</c> or the <c>aud</c> of the <c>id_token_hint</c>, or null if
///     the request names none or the Client of the hint no longer exists.
/// </param>
/// <param name="PostLogoutRedirectUri">The registered Post-logout Redirect URI to send the browser to, or null.</param>
/// <param name="State">The <c>state</c> to return with the redirect, or null.</param>
/// <param name="Hint">The valid <c>id_token_hint</c>, or null if none was sent.</param>
public sealed record ValidatedEndSessionRequest(
    Client? Client,
    string? PostLogoutRedirectUri,
    string? State,
    IdTokenHint? Hint)
{
    /// <summary>
    ///     Whether the <c>id_token_hint</c> was issued in the given Session: same User and same sign-in. Only then may
    ///     the User be signed out without confirmation; otherwise anyone could sign Users out with a link.
    /// </summary>
    /// <param name="userId">The User of the current Session.</param>
    /// <param name="authenticatedAt">When the User signed in to the current Session (UTC).</param>
    /// <returns>True if the hint belongs to the Session.</returns>
    public bool IsHintFor(Guid userId, DateTime authenticatedAt)
    {
        // auth_time has whole seconds, like the Session's sign-in time in its claims.
        return Hint is not null
            && Hint.Subject == userId
            && ToUnixTime(Hint.AuthenticatedAt) == ToUnixTime(authenticatedAt);
    }

    private static long ToUnixTime(DateTime value) => new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds();
}
