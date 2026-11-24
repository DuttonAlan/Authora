// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     A running sign-in or link through an external identity provider, kept by the browser in an encrypted,
///     short-lived cookie between the authorization request and the response of the provider.
/// </summary>
/// <param name="ProviderId">Id of the provider.</param>
/// <param name="Slug">Slug of the provider, which the response must arrive for.</param>
/// <param name="State">The <c>state</c> of the authorization request.</param>
/// <param name="Nonce">The <c>nonce</c> the ID Token must contain.</param>
/// <param name="CodeVerifier">The PKCE code verifier.</param>
/// <param name="ReturnUrl">The local URL to continue with afterwards.</param>
/// <param name="LinkSessionId">For a link: the Session of the signed-in User that started it; null for a sign-in.</param>
public sealed record ExternalSignInFlow(
    Guid ProviderId,
    string Slug,
    string State,
    string Nonce,
    string CodeVerifier,
    string ReturnUrl,
    Guid? LinkSessionId)
{
    /// <summary>
    ///     Whether the flow links an account to the signed-in User instead of signing in.
    /// </summary>
    public bool IsLink => LinkSessionId is not null;

    /// <summary>
    ///     Returns a description without the state, the nonce and the verifier.
    /// </summary>
    /// <returns>A description of the flow.</returns>
    public override string ToString() => $"ExternalSignInFlow(Slug = {Slug}, IsLink = {IsLink})";
}
