// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.OAuth.ClientAuthentication;

namespace Authora.Core.Application.OAuth.Token;

/// <summary>
///     The parameters of a request to the /token endpoint.
/// </summary>
/// <param name="GrantType">The <c>grant_type</c>: <c>authorization_code</c> or <c>refresh_token</c>.</param>
/// <param name="Code">The Authorization Code (<c>authorization_code</c> only).</param>
/// <param name="RedirectUri">The <c>redirect_uri</c> of the authorization request (<c>authorization_code</c> only).</param>
/// <param name="CodeVerifier">The PKCE <c>code_verifier</c> (<c>authorization_code</c> only).</param>
/// <param name="RefreshToken">The Refresh Token (<c>refresh_token</c> only).</param>
/// <param name="Scope">The requested Scopes: narrower ones for <c>refresh_token</c>, the token's Scopes for <c>client_credentials</c>.</param>
/// <param name="Credentials">The credentials of the Client.</param>
/// <param name="Resources">
///     The <c>resource</c> values (RFC 8707): the APIs the Access Token is meant for. Null or empty if none were sent.
/// </param>
public sealed record TokenRequest(
    string? GrantType,
    string? Code,
    string? RedirectUri,
    string? CodeVerifier,
    string? RefreshToken,
    string? Scope,
    ClientCredentials Credentials,
    IReadOnlyList<string>? Resources = null)
{
    /// <summary>
    ///     Returns a description without the code, verifier and tokens, so they are not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"TokenRequest({GrantType}, {Credentials})";
}
