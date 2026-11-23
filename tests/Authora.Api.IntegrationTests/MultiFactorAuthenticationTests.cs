// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Authora.Api.Authentication;
using Authora.Api.Pages.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Mfa;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Mfa;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks multi-factor authentication with an authenticator app (TOTP) and recovery codes: the setup on the
///     account pages, the second step of the sign-in, replay and lockout, removal, and the reset by administrators.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed partial class MultiFactorAuthenticationTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    // Cookies are sent explicitly, so each request uses exactly the cookies of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task Enrollment_ShowsQrCodeAndSecret_AndConfirmingWithACode_TurnsItOnWithRecoveryCodes()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var page = await GetAsync(AuthenticatorModel.PagePath, sessionToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("<svg class=\"qr\"", html, StringComparison.Ordinal);
        Assert.Matches("<path d=\"(M[0-9]+ [0-9]+h[0-9]+v1h-[0-9]+z)+\"", html);
        Assert.DoesNotContain("style=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
        Assert.True(page.Headers.CacheControl?.NoStore);

        var secret = ReadSecret(html);
        Assert.Equal(Totp.SecretLength, secret.Length);

        // Not confirmed yet: the password alone still signs in.
        var pending = await GetAuthenticatorAsync(userId);
        Assert.NotNull(pending);
        Assert.False(pending.IsConfirmed);
        Assert.DoesNotContain(Base32.Encode(secret), pending.ProtectedSecret, StringComparison.Ordinal);

        // Loading the page again keeps the secret the User may already have scanned.
        Assert.Equal(secret, ReadSecret(await GetPageAsync(AuthenticatorModel.PagePath, sessionToken)));

        using var confirmed = await PostAsync(AuthenticatorModel.PagePath, sessionToken, ("Code", CurrentCode(secret)));
        var confirmation = await confirmed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        var recoveryCodes = RecoveryCodePattern().Matches(confirmation).Select(match => match.Value).ToList();
        Assert.Equal(RecoveryCodeFormat.CountPerSet, recoveryCodes.Distinct().Count());

        var authenticator = await GetAuthenticatorAsync(userId);
        Assert.NotNull(authenticator);
        Assert.True(authenticator.IsConfirmed);
        var step = Totp.GetTimeStep(DateTimeOffset.UtcNow);
        Assert.InRange(authenticator.LastUsedTimeStep!.Value, step - 1, step + 1);
        Assert.Equal(RecoveryCodeFormat.CountPerSet, (await GetRecoveryCodesAsync(userId)).Count);

        var enrolled = Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.Enrolled, userId));
        Assert.Equal("Authenticator app, 10 recovery codes", enrolled.Details);

        var overview = await GetPageAsync(SecurityModel.PagePath, sessionToken);
        Assert.Contains("Authenticator app: on", overview, StringComparison.Ordinal);
        Assert.Contains("10 of 10 left", overview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enrollment_WithWrongCode_StaysOff()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var secret = ReadSecret(await GetPageAsync(AuthenticatorModel.PagePath, sessionToken));

        using var response = await PostAsync(AuthenticatorModel.PagePath, sessionToken, ("Code", WrongCode(secret)));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The code is incorrect.", html, StringComparison.Ordinal);
        Assert.Equal(secret, ReadSecret(html));
        Assert.False((await GetAuthenticatorAsync(userId))!.IsConfirmed);
        Assert.Empty(await GetRecoveryCodesAsync(userId));

        // A wrong first code does not count towards the lockout: the User is signed in and has the secret anyway.
        Assert.Equal(0, (await GetUserAsync(userId)).FailedLoginCount);
    }

    [Fact]
    public async Task Enrollment_WhenAlreadyEnrolled_OffersRemovalInstead()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);

        var html = await GetPageAsync(AuthenticatorModel.PagePath, sessionToken);

        Assert.Contains("already set up", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<svg", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuthenticatorModel.PagePath)]
    [InlineData(RemoveAuthenticatorModel.PagePath)]
    [InlineData(RecoveryCodesModel.PagePath)]
    public async Task SensitivePages_WithoutRecentAuthentication_AskToSignInAgain(string path)
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var sessionToken = await factory.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-30));

        using var response = await GetAsync(path, sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"{SessionAuthenticationHandler.SignInPath}?returnUrl=", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Null(await GetAuthenticatorAsync(userId));
    }

    [Fact]
    public async Task SignIn_WithCodeOfTheApp_StartsSessionWithOtp()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);

        var pendingToken = await StartSignInAsync(email);
        Assert.Empty(await GetSessionsAsync(userId));

        using var response = await SubmitCodeAsync(pendingToken, CurrentCode(secret));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(ReadCookie(response, SessionCookie.Name));

        var session = Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal($"{AuthenticationMethods.Password} {AuthenticationMethods.OneTimeCode}", session.AuthenticationMethod);

        var verified = Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.Verified, userId));
        Assert.Equal(AuditOutcome.Succeeded, verified.Outcome);
        Assert.Equal("Code of the authenticator app", verified.Details);

        Assert.NotNull((await GetAuthenticatorAsync(userId))!.LastUsedAt);
    }

    [Fact]
    public async Task SignInPage_OffersRecoveryCodes()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var pendingToken = await StartSignInAsync(email);

        using var request = new HttpRequestMessage(HttpMethod.Get, SecondFactorModel.PagePath);
        request.Headers.Add("Cookie", $"{PendingSignInCookie.Name}={pendingToken}");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(TotpSecondFactor.Name, html, StringComparison.Ordinal);
        Assert.Contains("recovery codes", html, StringComparison.Ordinal);
        Assert.DoesNotContain("inputmode=\"numeric\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_WithCodeThatWasAlreadyUsed_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var code = CurrentCode(secret);

        using (var first = await SubmitCodeAsync(await StartSignInAsync(email), code))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        using var replay = await SubmitCodeAsync(await StartSignInAsync(email), code);
        var html = await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains(SecondFactorModel.WrongResponseMessage, html, StringComparison.Ordinal);
        Assert.Null(ReadCookie(replay, SessionCookie.Name));
        Assert.Single(await GetSessionsAsync(userId));
        Assert.Equal(1, (await GetUserAsync(userId)).FailedLoginCount);

        var failure = (await GetEventsAsync(AuditEventTypes.Mfa.Verified, userId)).Single(auditEvent => auditEvent.Outcome == AuditOutcome.Failed);
        Assert.Equal("The code of the authenticator app was already used", failure.Details);
    }

    [Fact]
    public async Task SignIn_WithCodeOfAnEarlierTimeStepThanTheLastUsedOne_IsRejected()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var step = Totp.GetTimeStep(DateTimeOffset.UtcNow);

        using (var current = await SubmitCodeAsync(await StartSignInAsync(email), Totp.ComputeCode(secret, step)))
        {
            Assert.Equal(HttpStatusCode.Redirect, current.StatusCode);
        }

        // Still within the allowed drift, but older than the code that was just used.
        using var earlier = await SubmitCodeAsync(await StartSignInAsync(email), Totp.ComputeCode(secret, step - 1));

        Assert.Equal(HttpStatusCode.OK, earlier.StatusCode);
        Assert.Null(ReadCookie(earlier, SessionCookie.Name));
    }

    [Fact]
    public async Task SignIn_WithRecoveryCode_StartsSessionWithOtp_AndUsesUpTheCode()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (_, recoveryCodes) = await factory.EnrollTotpAsync(userId);

        // Case and dashes do not matter.
        var entered = recoveryCodes[0].Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        using var response = await SubmitCodeAsync(await StartSignInAsync(email), entered);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("pwd otp", Assert.Single(await GetSessionsAsync(userId)).AuthenticationMethod);

        var used = Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.RecoveryCodeUsed, userId));
        Assert.Equal("9 recovery codes left", used.Details);

        var codes = await GetRecoveryCodesAsync(userId);
        Assert.Single(codes, code => code.UsedAt is not null);

        using var again = await SubmitCodeAsync(await StartSignInAsync(email), recoveryCodes[0]);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Null(ReadCookie(again, SessionCookie.Name));
        Assert.Contains(
            await GetEventsAsync(AuditEventTypes.Mfa.Verified, userId),
            auditEvent => auditEvent.Outcome == AuditOutcome.Failed && auditEvent.Details == "Wrong or already used recovery code");
    }

    [Fact]
    public async Task SignIn_WithTooManyWrongCodes_LocksTheUserOut()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var pendingToken = await StartSignInAsync(email);

        for (var attempt = 1; attempt < 5; attempt++)
        {
            using var wrong = await SubmitCodeAsync(pendingToken, WrongCode(secret));
            Assert.Contains(SecondFactorModel.WrongResponseMessage, await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }

        using var lockedOut = await SubmitCodeAsync(pendingToken, WrongCode(secret));
        Assert.Contains(SecondFactorModel.LockedOutMessage, await lockedOut.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        Assert.True((await GetUserAsync(userId)).LockoutEndAt > DateTime.UtcNow);
        Assert.Single(await GetEventsAsync(AuditEventTypes.Account.LockedOut, userId));

        // Neither the password nor the correct code signs in while the User is locked out.
        using (var password = await SignInAsync(email, Password))
        {
            Assert.Null(ReadCookie(password, PendingSignInCookie.Name));
        }

        using var correct = await SubmitCodeAsync(pendingToken, CurrentCode(secret));
        Assert.Null(ReadCookie(correct, SessionCookie.Name));
        Assert.Empty(await GetSessionsAsync(userId));
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_InSessionWithPasswordOnly_RequiresStepUp()
    {
        // E.g. a Session that signed in before the app was set up: it must not get codes that would let it remove the
        // app without ever having it.
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var hashesBefore = (await GetRecoveryCodesAsync(userId)).Select(code => code.CodeHash).ToHashSet();

        var page = await GetPageAsync(RecoveryCodesModel.PagePath, sessionToken);
        Assert.Contains(RecoveryCodesModel.StepUpUrl.Replace("&", "&amp;", StringComparison.Ordinal), page, StringComparison.Ordinal);
        Assert.DoesNotContain("Generate new codes</button>", page, StringComparison.Ordinal);

        using var response = await PostAsync(RecoveryCodesModel.PagePath, sessionToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(RecoveryCodesModel.StepUpUrl, response.Headers.Location?.OriginalString);
        Assert.Equal(hashesBefore, (await GetRecoveryCodesAsync(userId)).Select(code => code.CodeHash).ToHashSet());
        Assert.Empty(await GetEventsAsync(AuditEventTypes.Mfa.RecoveryCodesRegenerated, userId));
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_AfterStepUp_Succeeds()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);

        // A Session that signed in with the password alone, then completes the app in a step-up.
        var sessionToken = await factory.CreateSessionAsync(userId);
        string pendingToken;

        using (var stepUp = await GetAsync(RecoveryCodesModel.StepUpUrl, sessionToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, stepUp.StatusCode);
            pendingToken = ReadCookie(stepUp, PendingSignInCookie.Name)!;
        }

        using (var completed = await SubmitCodeAsync(pendingToken, CurrentCode(secret), sessionToken))
        {
            Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
            sessionToken = ReadCookie(completed, SessionCookie.Name)!;
        }

        using var response = await PostAsync(RecoveryCodesModel.PagePath, sessionToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(RecoveryCodeFormat.CountPerSet, RecoveryCodePattern().Matches(html).Count);
        Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.RecoveryCodesRegenerated, userId));
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_ReplacesTheSet()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (_, oldCodes) = await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.OneTimeCode]);

        Assert.Contains("10 of 10", await GetPageAsync(RecoveryCodesModel.PagePath, sessionToken), StringComparison.Ordinal);

        using var response = await PostAsync(RecoveryCodesModel.PagePath, sessionToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var newCodes = RecoveryCodePattern().Matches(html).Select(match => match.Value).ToList();
        Assert.Equal(RecoveryCodeFormat.CountPerSet, newCodes.Count);
        Assert.Empty(newCodes.Intersect(oldCodes));
        Assert.Equal(RecoveryCodeFormat.CountPerSet, (await GetRecoveryCodesAsync(userId)).Count);
        Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.RecoveryCodesRegenerated, userId));

        using (var old = await SubmitCodeAsync(await StartSignInAsync(email), oldCodes[0]))
        {
            Assert.Null(ReadCookie(old, SessionCookie.Name));
        }

        using var fresh = await SubmitCodeAsync(await StartSignInAsync(email), newCodes[0]);
        Assert.NotNull(ReadCookie(fresh, SessionCookie.Name));
    }

    [Fact]
    public async Task Remove_WithCurrentCode_RemovesTheFactor_AndEndsTheOtherSessions()
    {
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var otherSessionToken = await factory.CreateSessionAsync(userId);

        using var response = await PostAsync(RemoveAuthenticatorModel.PagePath, sessionToken, ("Code", CurrentCode(secret)));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Authenticator app removed", html, StringComparison.Ordinal);
        Assert.Null(await GetAuthenticatorAsync(userId));
        Assert.Empty(await GetRecoveryCodesAsync(userId));

        var removed = Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.Removed, userId));
        Assert.Equal(AuditOutcome.Succeeded, removed.Outcome);

        // This Session stays valid, all others end.
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(SecurityModel.PagePath, sessionToken));
        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, otherSessionToken));

        // The password alone signs in again.
        using var signIn = await SignInAsync(email, Password);
        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
    }

    [Fact]
    public async Task Remove_AtTheSameTimeAsNewRecoveryCodes_LeavesNoCodesWithoutTheApp()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var removingSession = await factory.CreateSessionAsync(userId);
        var regeneratingSession = await factory.CreateSessionAsync(userId, authenticationMethods: [AuthenticationMethods.Password, AuthenticationMethods.OneTimeCode]);

        // Whichever runs first, codes must never outlive the app they stand in for.
        var removal = PostAsync(RemoveAuthenticatorModel.PagePath, removingSession, ("Code", CurrentCode(secret)));
        var regeneration = PostAsync(RecoveryCodesModel.PagePath, regeneratingSession);
        await Task.WhenAll(removal, regeneration);

        using (await removal)
        using (await regeneration)
        {
            Assert.Null(await GetAuthenticatorAsync(userId));
            Assert.Empty(await GetRecoveryCodesAsync(userId));
        }
    }

    [Fact]
    public async Task Remove_WithRecoveryCode_RemovesTheFactor()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (_, recoveryCodes) = await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var response = await PostAsync(RemoveAuthenticatorModel.PagePath, sessionToken, ("Code", recoveryCodes[3]));

        Assert.Contains("Authenticator app removed", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Null(await GetAuthenticatorAsync(userId));
        Assert.Empty(await GetRecoveryCodesAsync(userId));
    }

    [Fact]
    public async Task Remove_WithWrongCode_KeepsTheFactor_AndCountsTowardsTheLockout()
    {
        var (userId, _) = await factory.CreateUserWithPasswordAsync(Password);
        var (secret, _) = await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);

        using var response = await PostAsync(RemoveAuthenticatorModel.PagePath, sessionToken, ("Code", WrongCode(secret)));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(RemoveAuthenticatorModel.WrongCodeMessage, html, StringComparison.Ordinal);
        Assert.True((await GetAuthenticatorAsync(userId))!.IsConfirmed);
        Assert.Equal(1, (await GetUserAsync(userId)).FailedLoginCount);

        var failure = Assert.Single(await GetEventsAsync(AuditEventTypes.Mfa.Removed, userId));
        Assert.Equal(AuditOutcome.Failed, failure.Outcome);
    }

    [Fact]
    public async Task AdminApi_GetMfa_ReturnsTheStateWithoutSecrets()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.Read);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var (_, recoveryCodes) = await factory.EnrollTotpAsync(userId);

        using (var used = await SubmitCodeAsync(await StartSignInAsync(email), recoveryCodes[0]))
        {
            Assert.Equal(HttpStatusCode.Redirect, used.StatusCode);
        }

        using var response = await SendAsync(HttpMethod.Get, $"/api/users/{userId}/mfa", token);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var status = await response.Content.ReadFromJsonAsync<MfaStatusResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.True(status.Enabled);
        Assert.NotNull(status.AuthenticatorApp);
        Assert.Equal(9, status.RecoveryCodes?.Remaining);
        Assert.Equal(10, status.RecoveryCodes?.Total);
        Assert.NotNull(status.RecoveryCodes?.LastUsedAt);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);

        var withoutMfa = await factory.CreateUserAsync();

        using (var noneResponse = await SendAsync(HttpMethod.Get, $"/api/users/{withoutMfa}/mfa", token))
        {
            var none = await noneResponse.Content.ReadFromJsonAsync<MfaStatusResponse>(TestContext.Current.CancellationToken);
            Assert.NotNull(none);
            Assert.False(none.Enabled);
            Assert.Null(none.AuthenticatorApp);
            Assert.Null(none.RecoveryCodes);
            Assert.Empty(none.Passkeys);
        }

        using var unknown = await SendAsync(HttpMethod.Get, $"/api/users/{Guid.NewGuid()}/mfa", token);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task AdminApi_ResetMfa_RemovesTheFactor_AndRevokesSessionsAndGrants()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.ResetMfa);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        await factory.EnrollTotpAsync(userId);
        var sessionToken = await factory.CreateSessionAsync(userId);
        var accessToken = await factory.IssueAccessTokenAsync(userId, ["openid"]);
        var stampBefore = (await GetUserAsync(userId)).SecurityStamp;

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/mfa", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetAuthenticatorAsync(userId));
        Assert.Empty(await GetRecoveryCodesAsync(userId));
        Assert.NotEqual(stampBefore, (await GetUserAsync(userId)).SecurityStamp);
        Assert.Equal(HttpStatusCode.Redirect, await GetStatusAsync(SecurityModel.PagePath, sessionToken));

        using (var userInfo = await SendAsync(HttpMethod.Get, "/oauth/userinfo", accessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
        }

        var reset = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.MfaReset, userId));
        Assert.Equal("Authenticator app, 10 recovery codes, 0 passkeys removed", reset.Details);

        // The password alone signs in again, and a second reset changes nothing.
        using (var signIn = await SignInAsync(email, Password))
        {
            Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
        }

        using var again = await SendAsync(HttpMethod.Delete, $"/api/users/{userId}/mfa", token);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Single(await GetEventsAsync(AuditEventTypes.Users.MfaReset, userId));
    }

    [Fact]
    public async Task AdminApi_ResetMfa_OfAnAdministrator_IsForbiddenForNonAdministrators()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.ResetMfa);
        var administratorId = await factory.CreateAdministratorAsync();
        await factory.EnrollTotpAsync(administratorId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{administratorId}/mfa", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await GetAuthenticatorAsync(administratorId))!.IsConfirmed);

        var denied = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.MfaReset, administratorId));
        Assert.Equal(AuditOutcome.Failed, denied.Outcome);
    }

    [Fact]
    public async Task AdminApi_ResetMfa_OfAUserWithMorePermissions_IsForbidden()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.ResetMfa);
        var targetId = await factory.CreateUserAsync(PermissionNames.Users.Delete);
        await factory.EnrollTotpAsync(targetId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{targetId}/mfa", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await GetAuthenticatorAsync(targetId));
    }

    [Fact]
    public async Task AdminApi_ResetMfa_OfAnAdministrator_SucceedsForAdministrators()
    {
        var callerId = await factory.CreateAdministratorAsync();
        var token = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);
        var administratorId = await factory.CreateAdministratorAsync();
        await factory.EnrollTotpAsync(administratorId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{administratorId}/mfa", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetAuthenticatorAsync(administratorId));
    }

    [Fact]
    public async Task AdminApi_ResetMfa_WithoutPermission_IsForbidden()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.ManageAccess);
        var targetId = await factory.CreateUserAsync();
        await factory.EnrollTotpAsync(targetId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{targetId}/mfa", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await GetAuthenticatorAsync(targetId));
    }

    [Fact]
    public async Task DeleteUser_DeletesTheFactor()
    {
        var token = await CreateCallerTokenAsync(PermissionNames.Users.Delete);
        var targetId = await factory.CreateUserAsync();
        await factory.EnrollTotpAsync(targetId);

        using var response = await SendAsync(HttpMethod.Delete, $"/api/users/{targetId}", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetAuthenticatorAsync(targetId));
        Assert.Empty(await GetRecoveryCodesAsync(targetId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Acme:Login")]
    [InlineData("A name that is much longer than the sixty-four characters an authenticator app shows")]
    public void Options_RejectInvalidIssuerName(string issuerName)
    {
        Assert.True(new MfaOptionsValidator().Validate(null, new MfaOptions { IssuerName = issuerName }).Failed);
        Assert.True(new MfaOptionsValidator().Validate(null, new MfaOptions()).Succeeded);
    }

    [Fact]
    public void RecoveryCodes_AreNormalized()
    {
        Assert.True(RecoveryCodeFormat.TryNormalize(" abcd-efgh jkmn-pqrs ", out var code));
        Assert.Equal("ABCDEFGHJKMNPQRS", code);
        Assert.False(RecoveryCodeFormat.TryNormalize("ABCD-EFGH-JKMN-PQR0", out _));
        Assert.False(RecoveryCodeFormat.TryNormalize("123456", out _));
    }

    [GeneratedRegex("<code class=\"secret\">(?<secret>[A-Z2-7 ]+)</code>")]
    private static partial Regex SecretPattern();

    [GeneratedRegex("[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}")]
    private static partial Regex RecoveryCodePattern();

    private static byte[] ReadSecret(string html)
    {
        var match = SecretPattern().Match(html);
        Assert.True(match.Success, "The page shows no secret.");

        return Base32.Decode(match.Groups["secret"].Value.Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    private static string CurrentCode(byte[] secret) => Totp.ComputeCode(secret, Totp.GetTimeStep(DateTimeOffset.UtcNow));

    // A code that matches none of the accepted time steps.
    private static string WrongCode(byte[] secret)
    {
        var step = Totp.GetTimeStep(DateTimeOffset.UtcNow);
        var accepted = Enumerable.Range(-2, 5).Select(offset => Totp.ComputeCode(secret, step + offset)).ToHashSet();

        return Enumerable.Range(0, 10).Select(digit => new string((char)('0' + digit), Totp.Digits)).First(code => !accepted.Contains(code));
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

    // Signs in with the password and returns the token of the pending sign-in.
    private async Task<string> StartSignInAsync(string email)
    {
        using var response = await SignInAsync(email, Password);

        return ReadCookie(response, PendingSignInCookie.Name) ?? throw new InvalidOperationException("No pending sign-in was started.");
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(sessionToken: null);

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionAuthenticationHandler.SignInPath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("Email", email),
                new("Password", password),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", antiforgeryCookie);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // With a Session token for a step-up, which must be completed in its Session.
    private async Task<HttpResponseMessage> SubmitCodeAsync(string pendingToken, string code, string? sessionToken = null)
    {
        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(sessionToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, SecondFactorModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("Method", AuthenticationMethods.OneTimeCode),
                new("Code", code),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        var cookies = $"{PendingSignInCookie.Name}={pendingToken}; {antiforgeryCookie}";
        request.Headers.Add("Cookie", sessionToken is null ? cookies : $"{cookies}; {SessionCookie.Name}={sessionToken}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, string sessionToken, params (string Name, string Value)[] fields)
    {
        var (antiforgeryToken, antiforgeryCookie) = await GetAntiforgeryAsync(sessionToken);

        var form = fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToList();
        form.Add(new("__RequestVerificationToken", antiforgeryToken));

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("Cookie", $"{antiforgeryCookie}; {SessionCookie.Name}={sessionToken}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The antiforgery token is bound to the signed-in User, so it is read with the Session cookie the form is sent with.
    private async Task<(string Token, string Cookie)> GetAntiforgeryAsync(string? sessionToken)
    {
        using var page = await GetAsync($"{SessionAuthenticationHandler.SignInPath}?returnUrl=%2F", sessionToken);

        return await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (sessionToken is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}");
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

    private async Task<TotpAuthenticator?> GetAuthenticatorAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.TotpAuthenticators.AsNoTracking()
            .SingleOrDefaultAsync(authenticator => authenticator.UserId == userId, TestContext.Current.CancellationToken);
    }

    private async Task<List<RecoveryCode>> GetRecoveryCodesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.RecoveryCodes.AsNoTracking()
            .Where(code => code.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Session>> GetSessionsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Sessions.AsNoTracking()
            .Where(session => session.UserId == userId)
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
