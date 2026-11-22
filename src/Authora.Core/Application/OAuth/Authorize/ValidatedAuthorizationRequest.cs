// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;
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
/// <param name="CodeChallenge">
///     The PKCE code challenge (method <c>S256</c>), or null if the Client may omit PKCE and did, see
///     <see cref="Client.IsPkceOptional"/>.
/// </param>
/// <param name="Nonce">The <c>nonce</c> to return in the ID Token, or null.</param>
/// <param name="Prompts">The supported values of <c>prompt</c>, see <see cref="PromptValues"/>.</param>
/// <param name="MaxAge">The <c>max_age</c>: how long ago the User may have signed in at most, or null.</param>
/// <param name="AcrValues">
///     The values of <c>acr_values</c> Authora knows (<see cref="AuthenticationContextClasses"/>), in the requested
///     order. Empty if none was sent or none is known.
/// </param>
public sealed record ValidatedAuthorizationRequest(
    Client Client,
    string RedirectUri,
    string? RequestedRedirectUri,
    IReadOnlyList<Scope> Scopes,
    string? State,
    string? CodeChallenge,
    string? Nonce,
    IReadOnlySet<string> Prompts,
    TimeSpan? MaxAge,
    IReadOnlyList<string> AcrValues)
{
    /// <summary>
    ///     The weakest level of <see cref="AcrValues"/>: a Session that reaches it satisfies the request, because any
    ///     of the requested values is acceptable. Null if no known value was requested.
    /// </summary>
    public string? RequestedAcr => AcrValues.Count == 0
        ? null
        : AcrValues.MinBy(AuthenticationContextClasses.Rank);

    /// <summary>
    ///     Whether the User must be asked for consent: always for <c>prompt=consent</c>, never for a first-party
    ///     Client otherwise, and for a third-party Client unless the User's Consent already contains every requested
    ///     Scope.
    /// </summary>
    /// <param name="consent">The Consent of the User for the Client, or null if there is none.</param>
    /// <returns>True if the consent screen must be shown.</returns>
    public bool RequiresConsent(Consent? consent)
    {
        if (Prompts.Contains(PromptValues.Consent))
        {
            return true;
        }

        return !Client.IsFirstParty && (consent is null || !consent.Covers(Scopes.Select(scope => scope.Name)));
    }

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
