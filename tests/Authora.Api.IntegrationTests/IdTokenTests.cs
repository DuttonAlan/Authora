// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Authora.Api.Authentication;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Runs the Authorization Code flow with OpenID Connect: ID Tokens (US-023), consistent claims (US-026), and the
///     OpenID Connect parameters of the authorization request.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class IdTokenTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    // Cookies are sent explicitly per request. A cookie jar would resend the antiforgery cookie of an earlier page,
    // so the next page would not set a new one, and the Session cookie of one User would leak into the next request.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task OpenIdRequest_ReturnsSignedIdTokenWithRequiredClaims()
    {
        var userId = await factory.CreateUserAsync();
        var signedInAt = DateTime.UtcNow.AddMinutes(-10);
        var sessionToken = await factory.CreateSessionAsync(userId, signedInAt);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", "profile", "email");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid profile email");
        parameters["nonce"] = "test-nonce";

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (header, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        var jwks = await _client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", TestContext.Current.CancellationToken);
        var accessToken = tokens.GetProperty("access_token").GetString()!;
        var accessTokenHash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));

        // The first key of the JWKS is the signing key.
        Assert.Equal(jwks.GetProperty("keys")[0].GetProperty("kid").GetString(), header.GetProperty("kid").GetString());
        Assert.Equal(AuthoraApiFactory.Issuer, claims.GetProperty("iss").GetString());
        Assert.Equal(clientId.ToString(), claims.GetProperty("aud").GetString());
        Assert.Equal(userId.ToString(), claims.GetProperty("sub").GetString());
        Assert.Equal("test-nonce", claims.GetProperty("nonce").GetString());
        Assert.Equal(new DateTimeOffset(signedInAt).ToUnixTimeSeconds(), claims.GetProperty("auth_time").GetInt64());
        Assert.Equal(300, claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64());
        Assert.Equal(Base64Url.EncodeToString(accessTokenHash.AsSpan(0, 16)), claims.GetProperty("at_hash").GetString());
        Assert.Equal("Test User", claims.GetProperty("name").GetString());
        Assert.False(claims.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task IdTokenClaims_MatchUserInfo()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", "profile", "email");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid profile email"), sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, idTokenClaims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("access_token").GetString());
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var userInfo = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(userInfo.EnumerateObject(), claim => Assert.Equal(claim.Value.ToString(), idTokenClaims.GetProperty(claim.Name).ToString()));
    }

    [Fact]
    public async Task IdToken_IsNotAcceptedAsAccessToken()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("id_token").GetString());
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestWithoutOpenId_ReturnsNoIdToken()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("profile");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "profile"), sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        Assert.False(tokens.TryGetProperty("id_token", out _));
    }

    [Fact]
    public async Task Refresh_ReturnsNoIdToken()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var code = await AuthorizationCodeFlow.GetCodeAsync(
            _client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);

        using var content = new FormUrlEncodedContent(
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId.ToString()),
            new("refresh_token", tokens.GetProperty("refresh_token").GetString())
        ]);

        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);
        var refreshed = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(refreshed.TryGetProperty("id_token", out _));
    }

    [Fact]
    public async Task PromptNone_WithoutSession_RedirectsLoginRequired()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken: null);

        AssertRedirectedError(response, redirectUri, "login_required");
    }

    [Fact]
    public async Task PromptNone_WithSession_RedirectsConsentRequired()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedError(response, redirectUri, "consent_required");
    }

    [Fact]
    public async Task PromptNoneWithOtherValue_RedirectsInvalidRequest()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none login";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken: null);

        AssertRedirectedError(response, redirectUri, "invalid_request");
    }

    [Theory]
    [InlineData("request", "request_not_supported")]
    [InlineData("request_uri", "request_uri_not_supported")]
    [InlineData("registration", "registration_not_supported")]
    public async Task UnsupportedParameter_RedirectsItsError(string parameter, string error)
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters[parameter] = "value";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken: null);

        AssertRedirectedError(response, redirectUri, error);
    }

    [Fact]
    public async Task PromptLogin_WithSession_SendsToSignInAndBackWithoutPromptLogin()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "login consent";
        parameters["nonce"] = "test-nonce";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        var returnQuery = AssertSentToSignIn(response);
        Assert.Equal("consent", returnQuery["prompt"].Single());
        Assert.Equal("test-nonce", returnQuery["nonce"].Single());
    }

    [Fact]
    public async Task MaxAgeExceeded_SendsToSignInAndBackWithoutMaxAge()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync(), DateTime.UtcNow.AddMinutes(-10));
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["max_age"] = "60";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        var returnQuery = AssertSentToSignIn(response);
        Assert.False(returnQuery.ContainsKey("max_age"));
    }

    [Fact]
    public async Task MaxAgeNotExceeded_ShowsConsent()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync(), DateTime.UtcNow.AddMinutes(-10));
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["max_age"] = "3600";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("soon")]
    public async Task InvalidMaxAge_RedirectsInvalidRequest(string maxAge)
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["max_age"] = maxAge;

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken: null);

        AssertRedirectedError(response, redirectUri, "invalid_request");
    }

    [Fact]
    public async Task ApprovingAfterMaxAgeExceeded_SendsToSignInInsteadOfIssuingCode()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync(), DateTime.UtcNow.AddMinutes(-10));
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var shown = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        shown["max_age"] = "3600";

        // The submitted form is checked like a consent screen that stayed open until the sign-in was older than max_age.
        var submitted = new Dictionary<string, string?>(shown) { ["max_age"] = "60" };

        using var response = await AuthorizationCodeFlow.AllowAsync(_client, shown, submitted, sessionToken);

        var returnQuery = AssertSentToSignIn(response);
        Assert.False(returnQuery.ContainsKey("max_age"));
    }

    [Fact]
    public async Task NonceOfMaxLength_ShowsConsent()
    {
        var sessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync());
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["nonce"] = new string('n', 512);

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonceLongerThanMaxLength_RedirectsInvalidRequest()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["nonce"] = new string('n', 513);

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken: null);

        AssertRedirectedError(response, redirectUri, "invalid_request");
    }

    [Fact]
    public async Task SelectAccount_SigningInAsOtherUser_IssuesIdTokenForThatUserAndEndsPreviousSession()
    {
        const string password = "correct horse battery staple";

        var previousSessionToken = await factory.CreateSessionAsync(await factory.CreateUserAsync(), DateTime.UtcNow.AddMinutes(-10));
        var (otherUserId, otherEmail) = await factory.CreateUserWithPasswordAsync(password);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "select_account";

        var signInStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var authorize = await AuthorizationCodeFlow.GetAsync(_client, parameters, previousSessionToken);
        var returnQuery = AssertSentToSignIn(authorize);
        var signInUrl = authorize.Headers.Location!.OriginalString;

        // The signed-in User gets the sign-in form instead of "You are signed in".
        using var signInPage = await SendWithCookiesAsync(HttpMethod.Get, signInUrl, $"{SessionCookie.Name}={previousSessionToken}");
        var html = await signInPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, signInPage.StatusCode);
        Assert.Contains("asks you to sign in again", html, StringComparison.Ordinal);

        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(signInPage);

        using var signIn = await SendWithCookiesAsync(
            HttpMethod.Post,
            signInUrl,
            $"{SessionCookie.Name}={previousSessionToken}; {antiforgeryCookie}",
            new FormUrlEncodedContent(
            [
                new("Email", otherEmail),
                new("Password", password),
                new("__RequestVerificationToken", antiforgeryToken)
            ]));

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.StartsWith("/oauth/authorize?", signIn.Headers.Location!.OriginalString, StringComparison.Ordinal);

        var sessionToken = signIn.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])
            .Single(cookie => cookie.StartsWith($"{SessionCookie.Name}=", StringComparison.Ordinal))[(SessionCookie.Name.Length + 1)..];

        // Back at the request without select_account, the new Session of the other User approves it.
        var returnParameters = returnQuery.ToDictionary(parameter => parameter.Key, parameter => (string?)parameter.Value.Single());
        Assert.False(returnParameters.ContainsKey("prompt"));

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, returnParameters, sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        Assert.Equal(otherUserId.ToString(), claims.GetProperty("sub").GetString());
        Assert.True(claims.GetProperty("auth_time").GetInt64() >= signInStartedAt);

        // Signing in ended the Session of the previous User of the browser.
        using var withPreviousSession = await AuthorizationCodeFlow.GetAsync(
            _client,
            AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"),
            previousSessionToken);
        AssertSentToSignIn(withPreviousSession);
    }

    private async Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpMethod method,
        string url,
        string cookies,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Cookie", cookies);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void AssertRedirectedError(HttpResponseMessage response, string redirectUri, string error)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);

        var location = response.Headers.Location!;
        var query = QueryHelpers.ParseQuery(location.Query);

        Assert.StartsWith(redirectUri, location.ToString(), StringComparison.Ordinal);
        Assert.Equal(error, query["error"].Single());
        Assert.Equal("test-state", query["state"].Single());
        Assert.Equal(AuthoraApiFactory.Issuer, query["iss"].Single());
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> AssertSentToSignIn(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.StartsWith("/account/login", location.OriginalString, StringComparison.Ordinal);

        var returnUrl = QueryHelpers.ParseQuery(location.OriginalString[location.OriginalString.IndexOf('?', StringComparison.Ordinal)..])["returnUrl"].Single()!;
        Assert.StartsWith("/oauth/authorize?", returnUrl, StringComparison.Ordinal);

        return QueryHelpers.ParseQuery(returnUrl[returnUrl.IndexOf('?', StringComparison.Ordinal)..]);
    }
}
