// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The parameters of a request to the /authorize endpoint (RFC 6749, Section 4.1.1, RFC 7636, and OpenID Connect
///     Core 1.0, Section 3.1.2.1). Parameters sent without a value are treated as omitted. Unknown parameters are
///     ignored, e.g. <c>display</c>, <c>ui_locales</c> or <c>login_hint</c>.
/// </summary>
/// <param name="ClientId">The <c>client_id</c>.</param>
/// <param name="RedirectUri">The <c>redirect_uri</c>.</param>
/// <param name="ResponseType">The <c>response_type</c>. Only <c>code</c> is supported.</param>
/// <param name="Scope">The space-delimited <c>scope</c>.</param>
/// <param name="State">The <c>state</c>, returned to the Client unchanged.</param>
/// <param name="CodeChallenge">The PKCE <c>code_challenge</c>.</param>
/// <param name="CodeChallengeMethod">The PKCE <c>code_challenge_method</c>. Only <c>S256</c> is supported.</param>
/// <param name="ResponseMode">The <c>response_mode</c>. Only <c>query</c> is supported.</param>
/// <param name="Nonce">The OpenID Connect <c>nonce</c>, returned unchanged in the ID Token.</param>
/// <param name="Prompt">The OpenID Connect <c>prompt</c>, space-delimited.</param>
/// <param name="MaxAge">The OpenID Connect <c>max_age</c> in seconds, unparsed.</param>
/// <param name="UnsupportedParameter">
///     The first sent parameter of <see cref="UnsupportedParameterNames"/>, or null. Such requests must be rejected
///     instead of ignoring the parameter.
/// </param>
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
    string? Nonce,
    string? Prompt,
    string? MaxAge,
    string? UnsupportedParameter,
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
        "response_mode",
        "nonce",
        "prompt",
        "max_age"
    ];

    /// <summary>
    ///     OpenID Connect parameters Authora does not support. They change the meaning of the request, so OpenID
    ///     Connect Core 1.0, Section 3.1.2.6, requires an error instead of ignoring them.
    /// </summary>
    public static readonly IReadOnlyList<string> UnsupportedParameterNames = ["request", "request_uri", "registration"];

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
            Get("nonce"),
            Get("prompt"),
            Get("max_age"),
            UnsupportedParameterNames.FirstOrDefault(name => getValues(name).Any(value => !string.IsNullOrEmpty(value))),
            duplicates);
    }
}
