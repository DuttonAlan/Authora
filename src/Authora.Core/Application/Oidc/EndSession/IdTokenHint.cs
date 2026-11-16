// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     The claims of an <c>id_token_hint</c> that Authora issued, as far as logout needs them.
/// </summary>
/// <param name="Subject">The User (<c>sub</c>).</param>
/// <param name="ClientId">The Client the ID Token was issued to (<c>aud</c>).</param>
/// <param name="AuthenticatedAt">When the User signed in for the Session the token was issued in (<c>auth_time</c>, UTC).</param>
public sealed record IdTokenHint(Guid Subject, Guid ClientId, DateTime AuthenticatedAt);
