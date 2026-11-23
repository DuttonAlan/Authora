// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Authora.Api.Authentication;
using Authora.Api.Pages;
using Authora.Api.Pages.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks passkeys (WebAuthn) as a second factor with a software authenticator: the registration on the account
///     page, the second step of the sign-in, replayed and cloned responses, renaming and removal, and what
///     administrators see and remove.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class PasskeyTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // Cookies are sent explicitly, so each request uses exactly the cookies of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task PasskeysPage_AllowsOnlyScriptsOfItsOwnOrigin()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var page = await GetAsync(PasskeysModel.PagePath, sessionToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(SecurityHeadersPageFilter.ScriptContentSecurityPolicy, page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("<script type=\"module\" src=\"/js/passkeys.js?v=", html, StringComparison.Ordinal);
        Assert.Contains("data-passkey=\"create\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", html, StringComparison.Ordinal);

        // Pages without passkeys keep the policy without scripts.
        using var security = await GetAsync(SecurityModel.PagePath, sessionToken);
        Assert.Equal(SecurityHeadersPageFilter.ContentSecurityPolicy, security.Headers.GetValues("Content-Security-Policy").Single());

        using var script = await GetAsync("/js/passkeys.js", sessionToken: null);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Registration_StoresThePasskey()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator();

        var options = await GetRegistrationOptionsAsync(sessionToken);

        using (var document = JsonDocument.Parse(options))
        {
            var root = document.RootElement;
            Assert.Equal("localhost", root.GetProperty("rp").GetProperty("id").GetString());
            Assert.Equal(userId.ToByteArray(), Convert.FromBase64String(ToBase64(root.GetProperty("user").GetProperty("id").GetString()!)));
            Assert.Equal("none", root.GetProperty("attestation").GetString());
            Assert.Equal("preferred", root.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString());
        }

        using var response = await PostPageAsync(
            $"{PasskeysModel.PagePath}?handler=Register",
            sessionToken,
            pendingToken: null,
            ("Name", "  Security key  "),
            ("Credential", authenticator.Create(options, "usb", "nfc", "carrier-pigeon")));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Passkey &quot;Security key&quot; added.", html, StringComparison.Ordinal);

        var passkey = Assert.Single(await GetPasskeysAsync(userId));
        Assert.Equal("Security key", passkey.Name);
        Assert.Equal(authenticator.CredentialId, passkey.CredentialId);
        Assert.Equal(SoftwareAuthenticator.AaGuid, passkey.AaGuid);
        Assert.Equal("usb nfc", passkey.Transports);
        Assert.Equal(1, passkey.SignCount);
        Assert.False(passkey.IsBackupEligible);
        Assert.Null(passkey.LastUsedAt);

        var registered = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Registered, userId));
        Assert.Equal(AuditOutcome.Succeeded, registered.Outcome);
        Assert.Equal($"Passkey {passkey.Id} (hwk), AAGUID {SoftwareAuthenticator.AaGuid}", registered.Details);

        // The overview lists the passkey.
        Assert.Contains("Passkeys: 1 registered", await GetPageAsync(SecurityModel.PagePath, sessionToken), StringComparison.Ordinal);
        Assert.Contains("<strong>Security key</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_ReplayingTheResponse_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator();
        var credential = authenticator.Create(await GetRegistrationOptionsAsync(sessionToken));

        using (var first = await RegisterAsync(sessionToken, "Phone", credential))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        // In a Session with the passkey, as the User needs one now to change their passkeys.
        var mfaSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);
        using var replayed = await RegisterAsync(mfaSession, "Phone again", credential);
        var html = await replayed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(PasskeysModel.RejectedMessage, html, StringComparison.Ordinal);
        Assert.Single(await GetPasskeysAsync(userId));

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Registered, userId), auditEvent => auditEvent.Outcome == AuditOutcome.Failed);
        Assert.Equal("Unknown, used or expired challenge", failed.Details);
    }

    [Fact]
    public async Task Registration_OfTheSameAuthenticatorTwice_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator();

        await RegisterPasskeyAsync(sessionToken, authenticator, "Phone");

        // A Session with the passkey, as the User needs one now to change their passkeys.
        var mfaSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);
        var options = await GetRegistrationOptionsAsync(mfaSession);

        using (var document = JsonDocument.Parse(options))
        {
            var excluded = Assert.Single(document.RootElement.GetProperty("excludeCredentials").EnumerateArray());
            Assert.Equal(authenticator.CredentialId, Convert.FromBase64String(ToBase64(excluded.GetProperty("id").GetString()!)));
        }

        using var response = await RegisterAsync(mfaSession, "Phone again", authenticator.Create(options));

        Assert.Contains(PasskeysModel.AlreadyRegisteredMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Single(await GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task Registration_FromAnotherOrigin_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator { Origin = "https://phishing.example" };

        using var response = await RegisterAsync(sessionToken, "Phone", authenticator.Create(await GetRegistrationOptionsAsync(sessionToken)));

        Assert.Contains(PasskeysModel.RejectedMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetPasskeysAsync(userId));

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Registered, userId));
        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.Equal("Wrong origin", failed.Details);
    }

    [Fact]
    public async Task Registration_WithExpiredChallenge_IsRejected()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator();
        var options = await GetRegistrationOptionsAsync(sessionToken);

        await ExpireChallengesAsync(userId);

        using var response = await RegisterAsync(sessionToken, "Phone", authenticator.Create(options));

        Assert.Contains(PasskeysModel.RejectedMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetPasskeysAsync(userId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A name that is much longer than the sixty-four characters a passkey may have")]
    public async Task Registration_WithInvalidName_IsRejectedBeforeTheChallengeIsUsed(string name)
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        using var authenticator = new SoftwareAuthenticator();
        var credential = authenticator.Create(await GetRegistrationOptionsAsync(sessionToken));

        using (var response = await RegisterAsync(sessionToken, name, credential))
        {
            Assert.Contains("class=\"error\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Empty(await GetPasskeysAsync(userId));
        }

        // The challenge is still unused, so the User can correct the name.
        using var corrected = await RegisterAsync(sessionToken, "Phone", credential);
        Assert.Single(await GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task Registration_WithoutRecentAuthentication_AsksToSignInAgain()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-30));

        using var response = await GetAsync(PasskeysModel.PagePath, sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"{SessionAuthenticationHandler.SignInPath}?returnUrl=", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_InSessionWithoutTheSecondFactor_NeedsStepUp()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);

        // Signed in with the password alone, e.g. before the authenticator app was set up.
        var sessionToken = await factory.CreateSessionAsync(userId);

        var html = await GetPageAsync(PasskeysModel.PagePath, sessionToken);
        Assert.Contains($"href=\"{StepUpModel.PagePath}?returnUrl=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-passkey=\"create\"", html, StringComparison.Ordinal);

        using var options = await PostPageAsync($"{PasskeysModel.PagePath}?handler=RegistrationOptions", sessionToken, pendingToken: null);
        Assert.Equal(HttpStatusCode.Conflict, options.StatusCode);
        Assert.Empty(await GetChallengesAsync(userId));

        // With the code of the app in the Session, it works.
        var mfaSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.OneTimeCode]);
        using var authenticator = new SoftwareAuthenticator();
        await RegisterPasskeyAsync(mfaSession, authenticator, "Phone");
        Assert.Single(await GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task SignIn_WithPasskey_CompletesTheSecondStep()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);

        var pendingToken = await StartSignInAsync(email);

        using (var page = await GetAsync(SecondFactorModel.PagePath, sessionToken: null, pendingToken))
        {
            var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(SecurityHeadersPageFilter.ScriptContentSecurityPolicy, page.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("data-passkey=\"get\"", html, StringComparison.Ordinal);
            Assert.Contains("Use a passkey", html, StringComparison.Ordinal);

            // Only a passkey: no form for a code.
            Assert.DoesNotContain("autocomplete=\"one-time-code\"", html, StringComparison.Ordinal);
        }

        var options = await GetSignInOptionsAsync(pendingToken);

        using (var document = JsonDocument.Parse(options))
        {
            var allowed = Assert.Single(document.RootElement.GetProperty("allowCredentials").EnumerateArray());
            Assert.Equal(authenticator.CredentialId, Convert.FromBase64String(ToBase64(allowed.GetProperty("id").GetString()!)));
            Assert.Equal("localhost", document.RootElement.GetProperty("rpId").GetString());
        }

        using var response = await CompleteWithPasskeyAsync(pendingToken, authenticator.Get(options));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(ReadCookie(response, SessionCookie.Name));

        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal("pwd hwk", session.AuthenticationMethod);
        Assert.Equal(AuthenticationContextClasses.PhishingResistant, AuthenticationContextClasses.Of(session.AuthenticationMethods));

        var passkey = Assert.Single(await GetPasskeysAsync(userId));
        Assert.Equal(2, passkey.SignCount);
        Assert.NotNull(passkey.LastUsedAt);

        var used = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Used, userId));
        Assert.Equal($"Passkey {passkey.Id} (hwk)", used.Details);
        Assert.Equal(0, (await GetUserAsync(userId)).FailedLoginCount);
    }

    [Fact]
    public async Task SignIn_WithSyncedPasskey_RecordsASoftwareKey()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId, new SoftwareAuthenticator { Synced = true, Counts = false });

        Assert.True(Assert.Single(await GetPasskeysAsync(userId)).IsBackupEligible);

        // Twice: an authenticator that does not count always reports 0, which is accepted.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var pendingToken = await StartSignInAsync(email);

            using var response = await CompleteWithPasskeyAsync(pendingToken, authenticator.Get(await GetSignInOptionsAsync(pendingToken)));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.All(await GetSessionsAsync(userId), session => Assert.EndsWith("pwd swk", session.AuthenticationMethod, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_WithTotpAndPasskey_OffersBoth()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var mfaSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.OneTimeCode]);
        using var authenticator = new SoftwareAuthenticator();
        await RegisterPasskeyAsync(mfaSession, authenticator, "Phone");

        var pendingToken = await StartSignInAsync(email);

        using var page = await GetAsync(SecondFactorModel.PagePath, sessionToken: null, pendingToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("data-passkey=\"get\"", html, StringComparison.Ordinal);
        Assert.Contains("Or enter the code of your <strong>Authenticator app</strong>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Method\" value=\"otp\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ReplayedResponse_IsRejected_AndCountsTowardsTheLockout()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);

        var firstPending = await StartSignInAsync(email);
        var assertion = authenticator.Get(await GetSignInOptionsAsync(firstPending));

        using (var first = await CompleteWithPasskeyAsync(firstPending, assertion))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        var secondPending = await StartSignInAsync(email);
        using var replayed = await CompleteWithPasskeyAsync(secondPending, assertion);
        var html = await replayed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Contains(SecondFactorModel.WrongPasskeyMessage, html, StringComparison.Ordinal);
        Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal(1, (await GetUserAsync(userId)).FailedLoginCount);

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Failed, userId));
        Assert.Equal(AuditOutcome.Failed, failed.Outcome);
        Assert.Equal("Unknown, used or expired challenge", failed.Details);
    }

    [Fact]
    public async Task SignIn_SignCountThatDidNotIncrease_IsRejectedAsPossibleClone()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);

        var pendingToken = await StartSignInAsync(email);

        using (var first = await CompleteWithPasskeyAsync(pendingToken, authenticator.Get(await GetSignInOptionsAsync(pendingToken))))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        // A clone of the authenticator still has the counter of the time it was copied.
        authenticator.SignCount = 1;
        var clonePending = await StartSignInAsync(email);

        using var clone = await CompleteWithPasskeyAsync(clonePending, authenticator.Get(await GetSignInOptionsAsync(clonePending)));

        Assert.Equal(HttpStatusCode.OK, clone.StatusCode);
        Assert.Single(await GetSessionsAsync(userId));

        var passkey = Assert.Single(await GetPasskeysAsync(userId));
        Assert.Equal(2, passkey.SignCount);

        var failed = Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Failed, userId));
        Assert.Contains("the signature counter did not increase", failed.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_WithResponseOfAnotherOrigin_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var pendingToken = await StartSignInAsync(email);
        var options = await GetSignInOptionsAsync(pendingToken);

        authenticator.Origin = "https://phishing.example";
        using var response = await CompleteWithPasskeyAsync(pendingToken, authenticator.Get(options));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await GetSessionsAsync(userId));
        Assert.Contains("Wrong origin", Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Failed, userId)).Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_WithPasskeyOfAnotherUser_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var own = await CreateUserPasskeyAsync(userId);
        var (otherId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var other = await CreateUserPasskeyAsync(otherId);

        var pendingToken = await StartSignInAsync(email);
        using var response = await CompleteWithPasskeyAsync(pendingToken, other.Get(await GetSignInOptionsAsync(pendingToken)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await GetSessionsAsync(userId));
        Assert.Equal("Not a passkey of the User", Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Failed, userId)).Details);
    }

    [Fact]
    public async Task SignIn_WithChallengeOfARegistration_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var mfaSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);

        // The challenge of a registration, signed like a sign-in.
        using var document = JsonDocument.Parse(await GetRegistrationOptionsAsync(mfaSession));
        var forged = JsonSerializer.Serialize(new { challenge = document.RootElement.GetProperty("challenge").GetString(), rpId = "localhost" });

        var sessionsBefore = (await GetSessionsAsync(userId)).Count;
        var pendingToken = await StartSignInAsync(email);
        using var response = await CompleteWithPasskeyAsync(pendingToken, authenticator.Get(forged));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(sessionsBefore, (await GetSessionsAsync(userId)).Count);
        Assert.Equal("Unknown, used or expired challenge", Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Failed, userId)).Details);
    }

    [Fact]
    public async Task PasskeyOptions_WithoutPendingSignIn_AreNotFound()
    {
        using var response = await PostPageAsync($"{SecondFactorModel.PagePath}?handler=PasskeyOptions", sessionToken: null, pendingToken: "unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rename_ChangesTheName()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var passkey = Assert.Single(await GetPasskeysAsync(userId));
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);

        using var response = await PostPageAsync(
            $"{PasskeysModel.PagePath}?handler=Rename",
            sessionToken,
            pendingToken: null,
            ("PasskeyId", passkey.Id.ToString()),
            ("NewName", "Work laptop"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Work laptop", Assert.Single(await GetPasskeysAsync(userId)).Name);
        Assert.Equal($"Passkey {passkey.Id}", Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Renamed, userId)).Details);
    }

    [Fact]
    public async Task Rename_InSessionWithoutTheSecondFactor_NeedsStepUp()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var passkey = Assert.Single(await GetPasskeysAsync(userId));

        // Signed in with the password alone, e.g. before the passkey was registered.
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var response = await PostPageAsync(
            $"{PasskeysModel.PagePath}?handler=Rename",
            sessionToken,
            pendingToken: null,
            ("PasskeyId", passkey.Id.ToString()),
            ("NewName", "Work laptop"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(StepUpModel.PagePath, response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(passkey.Name, Assert.Single(await GetPasskeysAsync(userId)).Name);
        Assert.Empty(await GetEventsAsync(AuditEventTypes.Passkeys.Renamed, userId));
    }

    [Fact]
    public async Task Remove_DeletesThePasskey_AndSignsOutEverywhereElse()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var passkey = Assert.Single(await GetPasskeysAsync(userId));
        var otherSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);

        using var response = await PostPageAsync(
            $"{PasskeysModel.PagePath}?handler=Remove",
            sessionToken,
            pendingToken: null,
            ("PasskeyId", passkey.Id.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Passkey removed.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Empty(await GetPasskeysAsync(userId));
        Assert.Equal($"Passkey {passkey.Id}", Assert.Single(await GetEventsAsync(AuditEventTypes.Passkeys.Removed, userId)).Details?.Split(',')[0]);

        // This Session stays, the other ends.
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(SecurityModel.PagePath, sessionToken));
        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, otherSession));

        // The password alone signs in again.
        using var signIn = await SignInAsync(email, Password);
        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
    }

    [Fact]
    public async Task AdminApi_ListsAndRemovesPasskeys()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.Read, PermissionNames.Users.ResetMfa);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var first = await CreateUserPasskeyAsync(userId, name: "Phone");
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.HardwareKey]);
        using var second = new SoftwareAuthenticator { Synced = true };
        await RegisterPasskeyAsync(sessionToken, second, "Password manager");

        MfaStatusResponse? status;

        using (var response = await SendAsync(HttpMethod.Get, $"/api/users/{userId}/mfa", token))
        {
            var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            status = JsonSerializer.Deserialize<MfaStatusResponse>(json, JsonSerializerOptions.Web);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("publicKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialId", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotNull(status);
        Assert.True(status.Enabled);
        Assert.Null(status.AuthenticatorApp);
        Assert.Equal(["Phone", "Password manager"], status.Passkeys.Select(passkey => passkey.Name));
        Assert.Equal([false, true], status.Passkeys.Select(passkey => passkey.IsSynced));
        Assert.Equal(["usb"], status.Passkeys[0].Transports);

        var removedId = status.Passkeys[0].Id;

        using (var removed = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/passkeys/{removedId}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Equal("Password manager", Assert.Single(await GetPasskeysAsync(userId)).Name);
        Assert.Equal($"Passkey {removedId}", Assert.Single(await GetEventsAsync(AuditEventTypes.Users.PasskeyRemoved, userId)).Details);
        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, sessionToken));

        using var again = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/passkeys/{removedId}", token);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task AdminApi_RemovePasskey_WithoutPermission_IsForbidden()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.Read);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);
        var passkey = Assert.Single(await GetPasskeysAsync(userId));

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/passkeys/{passkey.Id}", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(await GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task AdminApi_ResetMfa_RemovesThePasskeys()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.ResetMfa);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.OneTimeCode]);
        using var authenticator = new SoftwareAuthenticator();
        await RegisterPasskeyAsync(sessionToken, authenticator, "Phone");

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/mfa", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetPasskeysAsync(userId));
        Assert.Equal("Authenticator app, 10 recovery codes, 1 passkeys removed", Assert.Single(await GetEventsAsync(AuditEventTypes.Users.MfaReset, userId)).Details);

        using var signIn = await SignInAsync(email, Password);
        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
    }

    [Fact]
    public async Task DeleteUser_DeletesThePasskeys()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.Delete);
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        using var authenticator = await CreateUserPasskeyAsync(userId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetPasskeysAsync(userId));
    }

    [Fact]
    public async Task Discovery_ReachesPhishingResistant()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", TestContext.Current.CancellationToken);

        Assert.Contains(
            AuthenticationContextClasses.PhishingResistant,
            document.GetProperty("acr_values_supported").EnumerateArray().Select(value => value.GetString()));
    }

    private static string ToBase64(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        return base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
    }

    // Registers a passkey for a User who has none yet, in a Session of their own.
    private async Task<SoftwareAuthenticator> CreateUserPasskeyAsync(Guid userId, SoftwareAuthenticator? authenticator = null, string name = "Security key")
    {
        authenticator ??= new SoftwareAuthenticator();
        var sessionToken = await factory.CreateSessionAsync(userId);

        await RegisterPasskeyAsync(sessionToken, authenticator, name);

        return authenticator;
    }

    private async Task RegisterPasskeyAsync(string sessionToken, SoftwareAuthenticator authenticator, string name)
    {
        using var response = await RegisterAsync(sessionToken, name, authenticator.Create(await GetRegistrationOptionsAsync(sessionToken), "usb"));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(html.Contains("added.", StringComparison.Ordinal), "The passkey was not registered.");
    }

    private async Task<string> GetRegistrationOptionsAsync(string sessionToken)
    {
        using var response = await PostPageAsync($"{PasskeysModel.PagePath}?handler=RegistrationOptions", sessionToken, pendingToken: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> RegisterAsync(string sessionToken, string name, string credential)
    {
        return PostPageAsync($"{PasskeysModel.PagePath}?handler=Register", sessionToken, pendingToken: null, ("Name", name), ("Credential", credential));
    }

    private async Task<string> GetSignInOptionsAsync(string pendingToken)
    {
        using var response = await PostPageAsync($"{SecondFactorModel.PagePath}?handler=PasskeyOptions", sessionToken: null, pendingToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> CompleteWithPasskeyAsync(string pendingToken, string assertion)
    {
        return PostPageAsync(SecondFactorModel.PagePath, sessionToken: null, pendingToken, ("Method", SecondFactorModel.PasskeyMethod), ("Code", assertion));
    }

    private async Task<string> StartSignInAsync(string email)
    {
        using var response = await SignInAsync(email, Password);

        return ReadCookie(response, PendingSignInCookie.Name) ?? throw new InvalidOperationException("No pending sign-in was started.");
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        return await PostPageAsync(SessionAuthenticationHandler.SignInPath, sessionToken: null, pendingToken: null, ("Email", email), ("Password", password));
    }

    private async Task<HttpResponseMessage> PostPageAsync(string path, string? sessionToken, string? pendingToken, params (string Name, string Value)[] fields)
    {
        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(sessionToken);

        var form = fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToList();
        form.Add(new("__RequestVerificationToken", antiforgeryToken));

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("Cookie", Cookies(sessionToken, pendingToken, antiforgeryCookie));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(string Token, string Cookie)> GetAntiforgeryAsync(string? sessionToken)
    {
        using var page = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2F", sessionToken);

        return await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken, string? pendingToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var cookies = Cookies(sessionToken, pendingToken, antiforgeryCookie: null);

        if (cookies.Length > 0)
        {
            request.Headers.Add("Cookie", cookies);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpStatusCode> GetStatusAsync(string path, string? sessionToken)
    {
        using var response = await GetAsync(path, sessionToken);

        return response.StatusCode;
    }

    private async Task<string> GetPageAsync(string path, string sessionToken)
    {
        using var response = await GetAsync(path, sessionToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string Cookies(string? sessionToken, string? pendingToken, string? antiforgeryCookie)
    {
        var cookies = new List<string>();

        if (sessionToken is not null)
        {
            cookies.Add($"{SessionCookie.Name}={sessionToken}");
        }

        if (pendingToken is not null)
        {
            cookies.Add($"{PendingSignInCookie.Name}={pendingToken}");
        }

        if (antiforgeryCookie is not null)
        {
            cookies.Add(antiforgeryCookie);
        }

        return string.Join("; ", cookies);
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

    private async Task<string> CreateCallerTokenAsync(params string[] permissions)
    {
        var callerId = await factory.CreateUserAsync(permissions);

        return await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task ExpireChallengesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var past = DateTime.UtcNow.AddMinutes(-1);

        await context.PasskeyChallenges
            .Where(challenge => challenge.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ExpiresAt, past), TestContext.Current.CancellationToken);
    }

    private async Task<List<PasskeyChallenge>> GetChallengesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.PasskeyChallenges.AsNoTracking()
            .Where(challenge => challenge.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Passkey>> GetPasskeysAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Passkeys.AsNoTracking()
            .Where(passkey => passkey.UserId == userId)
            .OrderBy(passkey => passkey.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Session>> GetSessionsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        // Only Sessions with a second factor: the password-only Sessions the tests create to register a passkey are left out.
        return await context.Sessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.AuthenticationMethod != "pwd")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<User> GetUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetEventsAsync(string type, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var target = userId.ToString();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == target)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
