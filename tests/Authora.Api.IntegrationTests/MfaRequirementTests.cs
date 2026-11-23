// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Domain.Entities;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Tests the <c>amr</c> and <c>acr</c> claims, <c>acr_values</c> with step-up, the Client setting "Require MFA" and
///     the requirement of multi-factor authentication for administrators.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class MfaRequirementTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private const string Code = "123456";

    private const string AcrPassword = "urn:authora:acr:pwd";

    private const string AcrMfa = "urn:authora:acr:mfa";

    // Cookies are sent explicitly per request, see IdTokenTests.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    public static TheoryData<string[], string[], string> AmrAndAcr => new()
    {
        { ["pwd"], ["pwd"], AcrPassword },
        { ["pwd", "otp"], ["pwd", "otp", "mfa"], AcrMfa },
        { ["pwd", "hwk"], ["pwd", "hwk", "mfa"], "phr" }
    };

    [Theory]
    [MemberData(nameof(AmrAndAcr))]
    public async Task IdToken_ContainsAmrAndAcrOfTheSession(string[] sessionMethods, string[] expectedAmr, string expectedAcr)
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: sessionMethods);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        Assert.Equal(expectedAmr, claims.GetProperty("amr").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(expectedAcr, claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task Discovery_ListsAcrValuesAndClaims()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        // Passkeys are a second factor, so phr can be reached.
        Assert.Equal([AcrPassword, AcrMfa, "phr"], document.GetProperty("acr_values_supported").EnumerateArray().Select(value => value.GetString()));

        var claims = document.GetProperty("claims_supported").EnumerateArray().Select(value => value.GetString()).ToList();
        Assert.Contains("amr", claims);
        Assert.Contains("acr", claims);
    }

    [Fact]
    public async Task AcrValues_SessionWithoutTheFactor_StepsUp()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = AcrMfa;

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedTo(response, StepUpModel.PagePath, parameters);
    }

    [Fact]
    public async Task AcrValues_StepUp_ThenIssuesCodeWithTheStrongerAcr()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = AcrMfa;

        string returnUrl;

        using (var authorize = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken))
        {
            var location = authorize.Headers.Location!.OriginalString;
            returnUrl = QueryHelpers.ParseQuery(location[location.IndexOf('?', StringComparison.Ordinal)..])["returnUrl"].ToString();
        }

        var steppedUpToken = await StepUpAsync(sessionToken, returnUrl);

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, parameters, steppedUpToken);

        // The fake factor is a smart card: the Session reaches the requested level.
        Assert.Equal(AcrMfa, claims.GetProperty("acr").GetString());
        Assert.Equal(["pwd", FakeSecondFactor.MethodName, "mfa"], claims.GetProperty("amr").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task AcrValues_UserWithoutSecondFactor_GetsTheLevelTheyReach()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = $"{AcrMfa} phr";

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, parameters, sessionToken);

        Assert.Equal(AcrPassword, claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task AcrValues_SatisfiedByTheWeakestRequestedValue_DoesNotStepUp()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = $"{AcrMfa} {AcrPassword}";

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, parameters, sessionToken);

        Assert.Equal(AcrPassword, claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task AcrValues_Unknown_AreIgnored()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = "1 2";

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, parameters, sessionToken);

        Assert.Equal(AcrPassword, claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task AcrValues_WithPromptNone_IsInteractionRequired()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["acr_values"] = AcrMfa;
        parameters["prompt"] = "none";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedError(response, redirectUri, "interaction_required");
    }

    [Fact]
    public async Task ClientRequiringMfa_SessionWithoutSecondFactor_StepsUp()
    {
        var (userId, _) = await CreateEnrolledUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await CreateClientRequiringMfaAsync();

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedTo(response, StepUpModel.PagePath, parameters);
    }

    [Fact]
    public async Task ClientRequiringMfa_UserWithoutSecondFactor_SetsOneUp()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await CreateClientRequiringMfaAsync();

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedTo(response, AuthenticatorModel.PagePath, parameters);
    }

    [Fact]
    public async Task ClientRequiringMfa_WithPromptNone_IsInteractionRequired()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await CreateClientRequiringMfaAsync();

        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");
        parameters["prompt"] = "none";

        using var response = await AuthorizationCodeFlow.GetAsync(_client, parameters, sessionToken);

        AssertRedirectedError(response, redirectUri, "interaction_required");
    }

    [Fact]
    public async Task ClientRequiringMfa_MultiFactorSession_GetsCode()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: ["pwd", "otp"]);
        var (clientId, redirectUri) = await CreateClientRequiringMfaAsync();

        var claims = await GetIdTokenClaimsAsync(clientId, redirectUri, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);

        Assert.Equal(AcrMfa, claims.GetProperty("acr").GetString());
    }

    [Fact]
    public async Task ClientRequiringMfa_CodeIssuedBeforeWithoutMfa_CannotBeExchanged()
    {
        var adminToken = await IssueAdminTokenAsync();
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid"), sessionToken);
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/mfa-required", adminToken));

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId.ToString(),
            ["code_verifier"] = AuthorizationCodeFlow.CodeVerifier
        });
        using var response = await _client.PostAsync("/oauth/token", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task RequireMfa_IsShownRecordedAndCanBeUndone()
    {
        var adminToken = await IssueAdminTokenAsync();
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.False(await GetRequiresMfaAsync(clientId, adminToken));

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/mfa-required", adminToken));
        Assert.True(await GetRequiresMfaAsync(clientId, adminToken));

        // Idempotent: no second event.
        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/mfa-required", adminToken));

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/clients/{clientId}/mfa-required", adminToken));
        Assert.False(await GetRequiresMfaAsync(clientId, adminToken));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var types = await context.AuditEvents
            .Where(auditEvent => auditEvent.TargetId == clientId.ToString())
            .OrderBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => auditEvent.Type)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([AuditEventTypes.Clients.MfaRequired, AuditEventTypes.Clients.MfaNoLongerRequired], types);
    }

    [Fact]
    public async Task RequireMfa_WithoutUpdatePermission_IsForbidden()
    {
        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(PermissionNames.Clients.Read), [AdminApiScope.Name]);
        var (clientId, _) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/mfa-required", token));
        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(HttpMethod.Put, $"/api/clients/{Guid.NewGuid()}/mfa-required", await IssueAdminTokenAsync()));
    }

    [Fact]
    public async Task AdminApi_WithoutAdministratorRequirement_AcceptsPasswordOnlyTokens()
    {
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        Assert.Equal(HttpStatusCode.OK, await SendAsync(HttpMethod.Get, "/api/me", token));
    }

    [Fact]
    public async Task AdminApi_AdministratorRequirement_RejectsTokensWithoutMfa()
    {
        await using var required = RequireMfaForAdministrators();
        using var client = required.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress });

        var userId = await factory.CreateAdministratorAsync();

        // Whichever Client the token was issued to.
        var passwordOnly = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name]);

        using (var response = await SendAsync(client, HttpMethod.Get, "/api/me", passwordOnly))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var challenge = Assert.Single(response.Headers.WwwAuthenticate);
            Assert.Contains("error=\"insufficient_user_authentication\"", challenge.Parameter, StringComparison.Ordinal);
            Assert.Contains($"acr_values=\"{AcrMfa}\"", challenge.Parameter, StringComparison.Ordinal);
        }

        var multiFactor = await factory.IssueAccessTokenAsync(userId, [AdminApiScope.Name], authenticationMethods: ["pwd", "otp"]);

        using (var response = await SendAsync(client, HttpMethod.Get, "/api/clients", multiFactor))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdminApi_AdministratorRequirement_DoesNotApplyToUsersWithoutPermissions()
    {
        await using var required = RequireMfaForAdministrators();
        using var client = required.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress });

        var token = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(), [AdminApiScope.Name]);

        using var response = await SendAsync(client, HttpMethod.Get, "/api/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_AdministratorWithoutSecondFactor_SetsOneUpRightAway()
    {
        await using var required = RequireMfaForAdministrators();
        using var client = required.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        await AssignAdministratorRoleAsync(userId);

        using var response = await SignInAsync(client, email, SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"{AuthenticatorModel.PagePath}?returnUrl={Uri.EscapeDataString(SecurityModel.PagePath)}",
            response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith($"{SessionCookie.Name}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_UserWithoutPermissions_IsNotAskedToSetUpAFactor()
    {
        await using var required = RequireMfaForAdministrators();
        using var client = required.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (_, email) = await factory.CreateUserWithPasswordAsync(Password);

        using var response = await SignInAsync(client, email, SecurityModel.PagePath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(SecurityModel.PagePath, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Authorize_AdminScope_AdministratorRequirement_StepsUpOrSetsUp()
    {
        await using var required = RequireMfaForAdministrators();
        using var client = required.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid", AdminApiScope.Name);
        var parameters = AuthorizationCodeFlow.Parameters(clientId, redirectUri, $"openid {AdminApiScope.Name}");

        var withoutFactor = await factory.CreateAdministratorAsync();

        using (var response = await AuthorizationCodeFlow.GetAsync(client, parameters, await factory.CreateSessionAsync(withoutFactor)))
        {
            AssertRedirectedTo(response, AuthenticatorModel.PagePath, parameters);
        }

        var withFactor = await factory.CreateAdministratorAsync();
        factory.SecondFactor.Enroll(withFactor, Code);

        using (var response = await AuthorizationCodeFlow.GetAsync(client, parameters, await factory.CreateSessionAsync(withFactor)))
        {
            AssertRedirectedTo(response, StepUpModel.PagePath, parameters);
        }

        // Without the admin Scope, the requirement does not apply.
        var openIdOnly = AuthorizationCodeFlow.Parameters(clientId, redirectUri, "openid");

        using (var response = await AuthorizationCodeFlow.GetAsync(client, openIdOnly, await factory.CreateSessionAsync(withoutFactor)))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Authenticator_WithReturnUrl_ContinuesThere()
    {
        var userId = await factory.CreateUserAsync();
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{AuthenticatorModel.PagePath}?returnUrl={Uri.EscapeDataString("/oauth/authorize?client_id=x")}");
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("To continue, your account needs a second step", page, StringComparison.Ordinal);
        Assert.Contains("value=\"/oauth/authorize?client_id=x\"", page, StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> RequireMfaForAdministrators()
    {
        return factory.WithWebHostBuilder(builder => builder.UseSetting("Authora:Mfa:RequireForAdministrators", "true"));
    }

    private async Task<(Guid Id, string Email)> CreateEnrolledUserAsync()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        factory.SecondFactor.Enroll(userId, Code);

        return (userId, email);
    }

    private async Task<(Guid ClientId, string RedirectUri)> CreateClientRequiringMfaAsync()
    {
        var (clientId, redirectUri) = await factory.CreatePublicClientAsync("openid");

        Assert.Equal(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Put, $"/api/clients/{clientId}/mfa-required", await IssueAdminTokenAsync()));

        return (clientId, redirectUri);
    }

    private async Task<string> IssueAdminTokenAsync()
    {
        return await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);
    }

    private async Task AssignAdministratorRoleAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var user = await context.Users.SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
        user.AssignRole(await context.Roles.SingleAsync(role => role.Name == Role.AdministratorName, TestContext.Current.CancellationToken));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Starts a step-up on its page and completes it with the fake factor; returns the new Session token.
    private async Task<string> StepUpAsync(string sessionToken, string returnUrl)
    {
        string pendingToken;

        using (var start = await GetAsync($"{StepUpModel.PagePath}?returnUrl={Uri.EscapeDataString(returnUrl)}", sessionToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            pendingToken = ReadCookie(start, PendingSignInCookie.Name)!;
        }

        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(_client, sessionToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, SecondFactorModel.PagePath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Method"] = FakeSecondFactor.MethodName,
                ["Code"] = Code,
                ["ReturnUrl"] = returnUrl,
                ["__RequestVerificationToken"] = antiforgeryToken
            })
        };
        request.Headers.Add("Cookie", $"{PendingSignInCookie.Name}={pendingToken}; {antiforgeryCookie}; {SessionCookie.Name}={sessionToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(returnUrl, response.Headers.Location?.OriginalString);

        return ReadCookie(response, SessionCookie.Name)!;
    }

    private async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string returnUrl)
    {
        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(client, sessionToken: null);

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionAuthenticationHandler.SignInPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = Password,
                ["ReturnUrl"] = returnUrl,
                ["__RequestVerificationToken"] = antiforgeryToken
            })
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The antiforgery token is bound to the signed-in User, so it is read with the Session cookie the form is sent with.
    private async Task<(string Token, string Cookie)> GetAntiforgeryAsync(HttpClient client, string? sessionToken)
    {
        using var page = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2F", sessionToken, client);

        return await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
        }

        return await (client ?? _client).SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetIdTokenClaimsAsync(
        Guid clientId,
        string redirectUri,
        Dictionary<string, string?> parameters,
        string sessionToken)
    {
        var code = await AuthorizationCodeFlow.GetCodeAsync(_client, parameters, sessionToken);
        var tokens = await AuthorizationCodeFlow.ExchangeAsync(_client, clientId, redirectUri, code);
        var (_, claims) = await AuthorizationCodeFlow.VerifyIdTokenAsync(_client, tokens.GetProperty("id_token").GetString()!);

        return claims;
    }

    private async Task<bool> GetRequiresMfaAsync(Guid clientId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/clients/{clientId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var client = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return client.GetProperty("requiresMfa").GetBoolean();
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string path, string token)
    {
        using var response = await SendAsync(_client, method, path, token);

        return response.StatusCode;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The User is sent to the page and from there back to the same authorization request.
    private static void AssertRedirectedTo(HttpResponseMessage response, string pagePath, Dictionary<string, string?> parameters)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location!;
        Assert.Equal(pagePath, location.OriginalString.Split('?')[0]);

        var returnUrl = QueryHelpers.ParseQuery(location.OriginalString[location.OriginalString.IndexOf('?', StringComparison.Ordinal)..])["returnUrl"].ToString();
        Assert.StartsWith("/oauth/authorize?", returnUrl, StringComparison.Ordinal);

        var returned = QueryHelpers.ParseQuery(returnUrl[returnUrl.IndexOf('?', StringComparison.Ordinal)..]);

        foreach (var (name, value) in parameters)
        {
            Assert.Equal(value, returned[name].ToString());
        }
    }

    private static void AssertRedirectedError(HttpResponseMessage response, string redirectUri, string error)
    {
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.StartsWith(redirectUri, response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(error, QueryHelpers.ParseQuery(response.Headers.Location.Query)["error"].ToString());
    }

    // The value of a cookie the response sets, or null if it sets none or deletes it.
    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var value = cookies
            .Select(cookie => cookie.Split(';')[0])
            .Where(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal))
            .Select(cookie => cookie[(name.Length + 1)..])
            .LastOrDefault();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
