// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     The parameters of an authorization request to an external identity provider.
/// </summary>
/// <param name="Issuer">The Issuer of the provider.</param>
/// <param name="ClientId">The Client Id of Authora at the provider.</param>
/// <param name="Scopes">The space-delimited Scopes.</param>
/// <param name="RedirectUri">The Redirect URI of Authora for the provider.</param>
/// <param name="State">The <c>state</c> value.</param>
/// <param name="Nonce">The <c>nonce</c> value the ID Token must contain.</param>
/// <param name="CodeChallenge">The S256 PKCE code challenge.</param>
public sealed record ExternalAuthorizationRequest(
    string Issuer,
    string ClientId,
    string Scopes,
    string RedirectUri,
    string State,
    string Nonce,
    string CodeChallenge)
{
    /// <summary>
    ///     Returns a description without the state and the nonce.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"ExternalAuthorizationRequest(Issuer = {Issuer}, ClientId = {ClientId})";
}

/// <summary>
///     An Authorization Code of an external identity provider to exchange, and what the ID Token must match.
/// </summary>
/// <param name="Issuer">The Issuer of the provider.</param>
/// <param name="ClientId">The Client Id of Authora at the provider.</param>
/// <param name="ClientSecret">The Client Secret of Authora at the provider in plain text, or null for a Public Client.</param>
/// <param name="Code">The Authorization Code.</param>
/// <param name="RedirectUri">The Redirect URI of the authorization request.</param>
/// <param name="CodeVerifier">The PKCE code verifier.</param>
/// <param name="Nonce">The <c>nonce</c> of the authorization request.</param>
public sealed record ExternalCodeRedemption(
    string Issuer,
    string ClientId,
    string? ClientSecret,
    string Code,
    string RedirectUri,
    string CodeVerifier,
    string Nonce)
{
    /// <summary>
    ///     Returns a description without the secret, the code, the verifier and the nonce.
    /// </summary>
    /// <returns>A description of the redemption.</returns>
    public override string ToString() => $"ExternalCodeRedemption(Issuer = {Issuer}, ClientId = {ClientId})";
}

/// <summary>
///     The verified claims of an ID Token of an external identity provider.
/// </summary>
/// <param name="Subject">The <c>sub</c> claim.</param>
/// <param name="Email">The <c>email</c> claim, or null.</param>
/// <param name="EmailVerified">The <c>email_verified</c> claim, false if missing.</param>
public sealed record ExternalIdentityClaims(string Subject, string? Email, bool EmailVerified);
