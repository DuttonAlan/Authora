// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Oidc.EndSession;

/// <summary>
///     The parameters of a request to the end-session endpoint (OpenID Connect RP-Initiated Logout 1.0, Section 2).
///     Parameters sent without a value are treated as omitted. Other parameters, e.g. <c>logout_hint</c> or
///     <c>ui_locales</c>, are ignored.
/// </summary>
/// <param name="IdTokenHint">The <c>id_token_hint</c>: an ID Token Authora issued to the Client, also an expired one.</param>
/// <param name="ClientId">The <c>client_id</c>.</param>
/// <param name="PostLogoutRedirectUri">The <c>post_logout_redirect_uri</c>.</param>
/// <param name="State">The <c>state</c>, returned unchanged with the redirect.</param>
/// <param name="DuplicateParameters">Names of the known parameters that were sent more than once.</param>
public sealed record EndSessionRequest(
    string? IdTokenHint,
    string? ClientId,
    string? PostLogoutRedirectUri,
    string? State,
    IReadOnlyCollection<string> DuplicateParameters)
{
    /// <summary>
    ///     Names of all parameters of a logout request that Authora reads.
    /// </summary>
    public static readonly IReadOnlyList<string> ParameterNames =
        ["id_token_hint", "client_id", "post_logout_redirect_uri", "state"];

    /// <summary>
    ///     Creates the request from its parameters.
    /// </summary>
    /// <param name="getValues">Returns all values of a parameter by its name, or an empty list if it was not sent.</param>
    /// <returns>The logout request.</returns>
    public static EndSessionRequest FromParameters(Func<string, IReadOnlyList<string?>> getValues)
    {
        ArgumentNullException.ThrowIfNull(getValues);

        var duplicates = new List<string>();

        string? Get(string name)
        {
            var values = getValues(name);

            if (values.Count > 1)
            {
                duplicates.Add(name);
            }

            return values.Count == 0 || string.IsNullOrEmpty(values[0]) ? null : values[0];
        }

        return new EndSessionRequest(
            Get("id_token_hint"),
            Get("client_id"),
            Get("post_logout_redirect_uri"),
            Get("state"),
            duplicates);
    }
}
