// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain;

namespace Authora.Core.Application.OAuth.Resources;

/// <summary>
///     A resource an Access Token can be meant for (RFC 8707): an API, or Authora itself.
/// </summary>
/// <param name="Id">The Id of the API's Client, or <see cref="AuthoraResource.Id"/> for Authora.</param>
/// <param name="Identifier">
///     The Resource Identifier, or the Client Id as text for a Client that is in an audience without being an API (any
///     more).
/// </param>
/// <param name="ServedScopes">
///     The Scope Names the resource serves: the served Scopes of the API, or the System Scopes for Authora.
/// </param>
public sealed record Resource(Guid Id, string Identifier, IReadOnlySet<string> ServedScopes)
{
    /// <summary>
    ///     Whether the resource serves at least one of the Scopes.
    /// </summary>
    /// <param name="scopes">The Scope Names.</param>
    /// <returns>True if one of them is served.</returns>
    public bool ServesAny(IEnumerable<string> scopes) => scopes.Any(ServedScopes.Contains);
}

/// <summary>
///     The audience and the Scopes of a new Access Token.
/// </summary>
/// <param name="Scopes">The Scopes of the token, cut to those its audience serves when resources were named.</param>
/// <param name="Audience">The Ids of the Clients the token is meant for, and <see cref="AuthoraResource.Id"/> for Authora.</param>
public sealed record TokenAudience(IReadOnlyList<string> Scopes, IReadOnlyList<Guid> Audience);
