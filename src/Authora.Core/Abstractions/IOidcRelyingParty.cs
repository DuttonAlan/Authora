// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.ExternalSignIn;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Authora as an OpenID Connect Relying Party of external identity providers: discovery,
///     the authorization request and the verified result of the token request (Authorization Code flow with PKCE).
/// </summary>
/// <remarks>
///     Implementations cache the discovery document and the keys of each Issuer, require HTTPS unless HTTP is allowed
///     for development, and never log or return tokens or secrets.
/// </remarks>
public interface IOidcRelyingParty
{
    /// <summary>
    ///     Fetches the discovery document of an Issuer once and checks that it can be used: its <c>issuer</c> equals the
    ///     Issuer, and it names an authorization endpoint, a token endpoint and a key set, and supports the response type
    ///     <c>code</c>.
    /// </summary>
    /// <param name="issuer">The Issuer.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Issuer is verified.</returns>
    /// <exception cref="ExternalSignInException">Thrown when the document cannot be fetched or is unusable.</exception>
    public Task ValidateIssuerAsync(string issuer, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates the URL of the authorization request at the provider.
    /// </summary>
    /// <param name="request">The parameters of the request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The URL to send the browser to.</returns>
    /// <exception cref="ExternalSignInException">Thrown when the discovery document cannot be fetched.</exception>
    public Task<Uri> CreateAuthorizationUrlAsync(ExternalAuthorizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exchanges an Authorization Code at the token endpoint and verifies the ID Token of the response: signature
    ///     (RS256, ES256 or PS256) with a key of the provider, <c>iss</c>, <c>aud</c> and <c>azp</c>, <c>exp</c> and
    ///     <c>iat</c>, and <c>nonce</c>.
    /// </summary>
    /// <param name="redemption">The code and what it must be redeemed with.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The verified claims of the ID Token.</returns>
    /// <exception cref="ExternalSignInException">Thrown when the exchange fails or the ID Token is invalid.</exception>
    public Task<ExternalIdentityClaims> RedeemCodeAsync(ExternalCodeRedemption redemption, CancellationToken cancellationToken = default);
}
