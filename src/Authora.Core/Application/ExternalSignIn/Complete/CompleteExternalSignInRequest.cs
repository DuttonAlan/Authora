// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn.Complete;

/// <summary>
///     The authorization response of an external identity provider, and the flow of the browser it must belong to.
/// </summary>
/// <param name="Flow">The flow from the cookie of the browser, or null if it has none or it expired.</param>
/// <param name="Slug">The Slug of the provider in the URL the response arrived at.</param>
/// <param name="State">The <c>state</c> parameter.</param>
/// <param name="Code">The <c>code</c> parameter.</param>
/// <param name="Error">The <c>error</c> parameter, e.g. <c>access_denied</c>.</param>
/// <param name="Issuer">The <c>iss</c> parameter (RFC 9207), or null if the provider does not send it.</param>
public sealed record CompleteExternalSignInRequest(
    ExternalSignInFlow? Flow,
    string Slug,
    string? State,
    string? Code,
    string? Error,
    string? Issuer)
{
    /// <summary>
    ///     Returns a description without the state and the code.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"CompleteExternalSignInRequest(Slug = {Slug}, Error = {Error})";
}
