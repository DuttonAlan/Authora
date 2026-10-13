// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Authora.Api.Authentication;

using Microsoft.AspNetCore.WebUtilities;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Drives the Authorization Code flow like a browser and a Client do, and verifies ID Tokens like a Client does.
/// </summary>
internal static partial class AuthorizationCodeFlow
{
    /// <summary>
    ///     The PKCE code verifier of all requests.
    /// </summary>
    public const string CodeVerifier = "test-code-verifier-with-at-least-43-characters-0123456789";

    /// <summary>
    ///     The PKCE code challenge of <see cref="CodeVerifier"/>.
    /// </summary>
    public static string CodeChallenge => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(CodeVerifier)));

    /// <summary>
    ///     Creates the parameters of a valid authorization request.
    /// </summary>
    /// <param name="clientId">The Id of the Client.</param>
    /// <param name="redirectUri">The Redirect URI of the Client.</param>
    /// <param name="scope">The space-delimited Scopes.</param>
    /// <returns>The parameters, which a test can change.</returns>
    public static Dictionary<string, string?> Parameters(Guid clientId, string redirectUri, string scope)
    {
        return new Dictionary<string, string?>
        {
            ["client_id"] = clientId.ToString(),
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = "test-state",
            ["code_challenge"] = CodeChallenge,
            ["code_challenge_method"] = "S256"
        };
    }

    /// <summary>
    ///     Sends the authorization request, optionally with a Session cookie.
    /// </summary>
    /// <param name="http">A client that does not follow redirects.</param>
    /// <param name="parameters">The parameters of the request.</param>
    /// <param name="sessionToken">The Session token, or null if not signed in.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> GetAsync(
        HttpClient http,
        Dictionary<string, string?> parameters,
        string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("/oauth/authorize", parameters));

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Opens the consent screen as a signed-in User, allows the request and returns the Authorization Code.
    /// </summary>
    /// <param name="http">A client that does not follow redirects.</param>
    /// <param name="parameters">The parameters of the request.</param>
    /// <param name="sessionToken">The Session token of the User.</param>
    /// <returns>The Authorization Code.</returns>
    public static async Task<string> GetCodeAsync(HttpClient http, Dictionary<string, string?> parameters, string sessionToken)
    {
        using var response = await AllowAsync(http, parameters, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);

        return query["code"].Single()!;
    }

    /// <summary>
    ///     Opens the consent screen as a signed-in User and allows the request with the given form parameters, which
    ///     may differ from the shown request, e.g. to submit a consent screen as if it was shown with other parameters.
    /// </summary>
    /// <param name="http">A client that does not follow redirects.</param>
    /// <param name="consentParameters">The parameters of the request that shows the consent screen.</param>
    /// <param name="formParameters">The request parameters submitted with the consent form.</param>
    /// <param name="sessionToken">The Session token of the User.</param>
    /// <returns>The response to the submitted form.</returns>
    public static async Task<HttpResponseMessage> AllowAsync(
        HttpClient http,
        Dictionary<string, string?> consentParameters,
        Dictionary<string, string?> formParameters,
        string sessionToken)
    {
        string antiforgeryToken;
        string antiforgeryCookie;

        using (var consent = await GetAsync(http, consentParameters, sessionToken))
        {
            Assert.Equal(HttpStatusCode.OK, consent.StatusCode);

            (antiforgeryToken, antiforgeryCookie) = await ReadAntiforgeryAsync(consent);
        }

        var form = formParameters
            .Where(parameter => parameter.Value is not null)
            .Append(new("decision", "allow"))
            .Append(new("__RequestVerificationToken", antiforgeryToken));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/authorize")
        {
            Content = new FormUrlEncodedContent(form!)
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");

        return await http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Reads the antiforgery token of the form on a page and the antiforgery cookie that belongs to it.
    /// </summary>
    /// <param name="page">The response with the page.</param>
    /// <returns>The token and the cookie as <c>name=value</c>.</returns>
    public static async Task<(string Token, string Cookie)> ReadAntiforgeryAsync(HttpResponseMessage page)
    {
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = WebUtility.HtmlDecode(AntiforgeryTokenPattern().Match(html).Groups[1].Value);
        var cookie = page.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])
            .Single(cookie => cookie.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));

        return (token, cookie);
    }

    /// <summary>
    ///     Exchanges an Authorization Code at the token endpoint as a Public Client.
    /// </summary>
    /// <param name="http">The client.</param>
    /// <param name="clientId">The Id of the Client.</param>
    /// <param name="redirectUri">The Redirect URI of the authorization request.</param>
    /// <param name="code">The Authorization Code.</param>
    /// <returns>The token response.</returns>
    public static async Task<JsonElement> ExchangeAsync(HttpClient http, Guid clientId, string redirectUri, string code)
    {
        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId.ToString()),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", CodeVerifier)
        ]);

        using var response = await http.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Verifies the signature of an ID Token with the key of the JWKS endpoint its header names, like a Client does.
    /// </summary>
    /// <param name="http">The client.</param>
    /// <param name="idToken">The ID Token.</param>
    /// <returns>The header and the claims of the token.</returns>
    public static async Task<(JsonElement Header, JsonElement Claims)> VerifyIdTokenAsync(HttpClient http, string idToken)
    {
        var parts = idToken.Split('.');
        Assert.Equal(3, parts.Length);

        var header = JsonSerializer.Deserialize<JsonElement>(Base64Url.DecodeFromChars(parts[0]));
        var claims = JsonSerializer.Deserialize<JsonElement>(Base64Url.DecodeFromChars(parts[1]));

        Assert.Equal("RS256", header.GetProperty("alg").GetString());

        var jwks = await http.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", TestContext.Current.CancellationToken);
        var key = jwks.GetProperty("keys").EnumerateArray()
            .Single(key => key.GetProperty("kid").GetString() == header.GetProperty("kid").GetString());

        using var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(key.GetProperty("n").GetString()),
            Exponent = Base64Url.DecodeFromChars(key.GetProperty("e").GetString())
        });

        Assert.True(rsa.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));

        return (header, claims);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();
}
