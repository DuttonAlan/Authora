// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     An authorization request that passed all checks and can be shown to the User for consent.
/// </summary>
/// <param name="Client">The requesting Client.</param>
/// <param name="RedirectUri">The registered Redirect URI the response is sent to.</param>
/// <param name="RequestedRedirectUri">The <c>redirect_uri</c> as sent, or null if it was omitted.</param>
/// <param name="Scopes">The requested Scopes, in the requested order.</param>
/// <param name="State">The <c>state</c> to return unchanged, or null.</param>
/// <param name="CodeChallenge">The PKCE code challenge (method <c>S256</c>).</param>
/// <param name="Nonce">The <c>nonce</c> to return in the ID Token, or null.</param>
/// <param name="Prompts">The supported values of <c>prompt</c>, see <see cref="PromptValues"/>.</param>
/// <param name="MaxAge">The <c>max_age</c>: how long ago the User may have signed in at most, or null.</param>
public sealed record ValidatedAuthorizationRequest(
    Client Client,
    string RedirectUri,
    string? RequestedRedirectUri,
    IReadOnlyList<Scope> Scopes,
    string? State,
    string CodeChallenge,
    string? Nonce,
    IReadOnlySet<string> Prompts,
    TimeSpan? MaxAge)
{
    /// <summary>
    ///     Whether the User must be asked for consent: always, unless the Client is first-party (TD-012) and did not
    ///     send <c>prompt=consent</c>.
    /// </summary>
    public bool RequiresConsent => !Client.IsFirstParty || Prompts.Contains(PromptValues.Consent);

    /// <summary>
    ///     Whether the User must sign in again, even if signed in: <c>prompt=login</c> or <c>prompt=select_account</c>
    ///     was sent, or the sign-in was longer ago than <see cref="MaxAge"/>.
    /// </summary>
    /// <param name="authenticatedAt">When the User signed in (UTC).</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>True if the User must sign in again.</returns>
    public bool RequiresSignIn(DateTime authenticatedAt, DateTime now)
    {
        return Prompts.Contains(PromptValues.Login)
            || Prompts.Contains(PromptValues.SelectAccount)
            || (MaxAge is not null && now - authenticatedAt > MaxAge);
    }
}
