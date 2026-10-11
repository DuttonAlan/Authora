// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth.ClientAuthentication;

/// <summary>
///     The credentials a Client sent to the token, revocation or introspection endpoint, either in the
///     Authorization header (<c>client_secret_basic</c>) or in the request body (<c>client_secret_post</c>).
/// </summary>
/// <param name="ClientId">The <c>client_id</c>, or null if it was not sent.</param>
/// <param name="ClientSecret">The <c>client_secret</c>, or null for Public Clients.</param>
public sealed record ClientCredentials(string? ClientId, string? ClientSecret)
{
    /// <summary>
    ///     Returns a description without the Secret, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the credentials.</returns>
    public override string ToString() => $"ClientCredentials({ClientId}, ***)";
}
