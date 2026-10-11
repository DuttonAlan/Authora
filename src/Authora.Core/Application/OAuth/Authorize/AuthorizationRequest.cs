// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The parameters of a request to the /authorize endpoint (RFC 6749, Section 4.1.1, and RFC 7636).
///     Parameters sent without a value are treated as omitted. Unknown parameters are ignored.
/// </summary>
/// <param name="ClientId">The <c>client_id</c>.</param>
/// <param name="RedirectUri">The <c>redirect_uri</c>.</param>
/// <param name="ResponseType">The <c>response_type</c>. Only <c>code</c> is supported.</param>
/// <param name="Scope">The space-delimited <c>scope</c>.</param>
/// <param name="State">The <c>state</c>, returned to the Client unchanged.</param>
/// <param name="CodeChallenge">The PKCE <c>code_challenge</c>.</param>
/// <param name="CodeChallengeMethod">The PKCE <c>code_challenge_method</c>. Only <c>S256</c> is supported.</param>
/// <param name="ResponseMode">The <c>response_mode</c>. Only <c>query</c> is supported.</param>
/// <param name="DuplicateParameters">Names of the known parameters that were sent more than once.</param>
public sealed record AuthorizationRequest(
    string? ClientId,
    string? RedirectUri,
    string? ResponseType,
    string? Scope,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? ResponseMode,
    IReadOnlyCollection<string> DuplicateParameters)
{
    /// <summary>
    ///     Names of all parameters of an authorization request that Authora reads.
    /// </summary>
    public static readonly IReadOnlyList<string> ParameterNames =
    [
        "client_id",
        "redirect_uri",
        "response_type",
        "scope",
        "state",
        "code_challenge",
        "code_challenge_method",
        "response_mode"
    ];

    /// <summary>
    ///     Creates the request from its parameters.
    /// </summary>
    /// <param name="getValues">Returns all values of a parameter by its name, or an empty list if it was not sent.</param>
    /// <returns>The authorization request.</returns>
    public static AuthorizationRequest FromParameters(Func<string, IReadOnlyList<string?>> getValues)
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

            // RFC 6749, Section 3.1: parameters sent without a value are treated as if they were omitted.
            return values.Count == 0 || string.IsNullOrEmpty(values[0]) ? null : values[0];
        }

        return new AuthorizationRequest(
            Get("client_id"),
            Get("redirect_uri"),
            Get("response_type"),
            Get("scope"),
            Get("state"),
            Get("code_challenge"),
            Get("code_challenge_method"),
            Get("response_mode"),
            duplicates);
    }
}
