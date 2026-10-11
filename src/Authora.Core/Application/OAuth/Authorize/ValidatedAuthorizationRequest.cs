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
public sealed record ValidatedAuthorizationRequest(
    Client Client,
    string RedirectUri,
    string? RequestedRedirectUri,
    IReadOnlyList<Scope> Scopes,
    string? State,
    string CodeChallenge);
