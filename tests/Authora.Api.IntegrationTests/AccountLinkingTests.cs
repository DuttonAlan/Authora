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
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;
using Authora.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks just-in-time provisioning through external identity providers (sign-up, allowed domains, default Groups),
///     linking an account whose Email belongs to a User only after a full sign-in to that User, Users without a
///     password, the guard for their last way to sign in, and the administration of linked accounts.
/// </summary>
/// <param name="factory">The Api under test.</param>
public sealed class AccountLinkingTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const string Password = "correct-horse-battery-staple";

    private const string Code = "135790";

    // Cookies are sent explicitly, so each request uses exactly the cookies of the test.
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = AuthoraApiFactory.BaseAddress, AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task SignUp_CreatesAUserWithoutPassword_InTheDefaultGroups()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        var groupId = await CreateGroupAsync();
        await ConfigureSignUpAsync(providerId, allowSignUp: true, ["corp.example"], [groupId]);
        var email = $"{Guid.NewGuid():N}@Corp.Example";
        provider.ChangeClaims = claims =>
        {
            claims["given_name"] = "Ada";
            claims["family_name"] = "Lovelace";
        };

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-signup", email), flowCookie);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(SecurityModel.PagePath, callback.Headers.Location?.OriginalString);
        Assert.NotNull(ReadCookie(callback, SessionCookie.Name));

        var user = await GetUserByEmailAsync(email);
        Assert.False(user.HasPassword);
        Assert.True(user.IsEmailVerified);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal([groupId], user.Groups.Select(group => group.Id));

        var identity = Assert.Single(await GetIdentitiesAsync(user.Id));
        Assert.Equal("subject-signup", identity.Subject);
        Assert.NotNull(identity.LastUsedAt);

        var session = Assert.Single(await GetSessionsAsync(user.Id));
        Assert.Equal("fed", session.AuthenticationMethod);

        var provisioned = Assert.Single(await GetEventsAsync(AuditEventTypes.Users.Provisioned, user.Id.ToString()));
        Assert.Contains($"identity provider '{slug}'", provisioned.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(groupId.ToString(), provisioned.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("subject-signup", provisioned.Details, StringComparison.Ordinal);

        // The next sign-in uses the link instead of creating another User.
        var (signInUrl, signInCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var signIn = await CallbackAsync(slug, provider.Authorize(signInUrl, "subject-signup", email), signInCookie);

        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
        Assert.Single(await GetEventsAsync(AuditEventTypes.Users.Provisioned, user.Id.ToString()));
    }

    [Fact]
    public async Task SignUp_WithoutNames_UsesTheLocalPartOfTheEmail()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        await ConfigureSignUpAsync(providerId, allowSignUp: true, [], []);
        var localPart = $"user{Guid.NewGuid():N}"[..20];

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-names", $"{localPart}@anywhere.example"), flowCookie);

        Assert.NotNull(ReadCookie(callback, SessionCookie.Name));
        var user = await GetUserByEmailAsync($"{localPart}@anywhere.example");
        Assert.Equal(localPart, user.FirstName);
        Assert.Equal(localPart, user.LastName);
        Assert.Empty(user.Groups);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("domain")]
    [InlineData("unverified")]
    [InlineData("no-email")]
    public async Task SignUp_IsRefused_WithTheSameMessageForEveryReason(string reason)
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        await ConfigureSignUpAsync(providerId, allowSignUp: reason != "off", ["corp.example"], []);
        var email = reason switch
        {
            "domain" => $"{Guid.NewGuid():N}@other.example",
            "no-email" => null,
            _ => $"{Guid.NewGuid():N}@corp.example"
        };

        if (reason == "unverified")
        {
            provider.ChangeClaims = claims => claims["email_verified"] = false;
        }

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, $"subject-refused-{reason}", email), flowCookie);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Null(ReadCookie(callback, SessionCookie.Name));
        Assert.Null(ReadCookie(callback, ExternalLinkCookie.Name));
        Assert.Contains(ExternalCallbackModel.GetNotPossibleMessage("Test IdP"), await ReadAsync(callback), StringComparison.Ordinal);

        if (email is not null)
        {
            Assert.False(await UserExistsAsync(email));
        }

        var failure = Assert.Single(await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()));
        Assert.Equal(AuditOutcome.Failed, failure.Outcome);
        Assert.Contains(
            reason switch
            {
                "off" => "sign-up is turned off",
                "domain" => "domain of the Email is not allowed",
                _ => "no verified Email"
            },
            failure.Details,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignUp_ThroughGoogle_ChecksTheWorkspaceDomain()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        await MakeGooglePresetAsync(providerId);
        await ConfigureSignUpAsync(providerId, allowSignUp: true, ["corp.example"], []);

        // A personal account has no hd, even with an Email in the domain.
        var personal = $"{Guid.NewGuid():N}@corp.example";
        var (personalUrl, personalCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var refused = await CallbackAsync(slug, provider.Authorize(personalUrl, "subject-personal", personal), personalCookie);

        Assert.Null(ReadCookie(refused, SessionCookie.Name));
        Assert.False(await UserExistsAsync(personal));
        Assert.Contains(
            await GetEventsAsync(AuditEventTypes.Account.ExternalSignInFailed, providerId.ToString()),
            failure => failure.Details?.Contains("Google Workspace domain (hd)", StringComparison.Ordinal) == true);

        provider.ChangeClaims = claims => claims["hd"] = "corp.example";
        var member = $"{Guid.NewGuid():N}@corp.example";
        var (memberUrl, memberCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var accepted = await CallbackAsync(slug, provider.Authorize(memberUrl, "subject-workspace", member), memberCookie);

        Assert.NotNull(ReadCookie(accepted, SessionCookie.Name));
        Assert.True(await UserExistsAsync(member));
    }

    [Fact]
    public void GooglePreset_CannotTurnOnSignUpWithoutADomain()
    {
        var provider = IdentityProvider.Create("Google", "google", IdentityProviderPreset.Google, null, "client-id");

        Assert.Throws<DomainValidationException>(() => provider.ConfigureSignUp(true, []));

        provider.ConfigureSignUp(true, ["Example.COM."]);
        Assert.Equal(["example.com"], provider.AllowedEmailDomains);
        Assert.True(provider.IsDomainAllowed("EXAMPLE.com"));
        Assert.False(provider.IsDomainAllowed(null));
        Assert.Throws<DomainValidationException>(() => provider.ConfigureSignUp(true, ["user@example.com"]));
    }

    [Fact]
    public async Task AdminApi_ConfiguresSignUp_AndRejectsGoogleWithoutADomain()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, _) = await factory.CreateIdentityProviderAsync(provider);
        var groupId = await CreateGroupAsync();
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var update = await SendAdminAsync(HttpMethod.Put, $"/api/identity-providers/{providerId}", token, new
        {
            allowSignUp = true,
            allowedEmailDomains = new[] { "corp.example" },
            defaultGroupIds = new[] { groupId }
        });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        using var get = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{providerId}", token);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.GetProperty("allowSignUp").GetBoolean());
        Assert.Equal(["corp.example"], body.GetProperty("allowedEmailDomains").EnumerateArray().Select(domain => domain.GetString()));
        Assert.Equal(groupId, Assert.Single(body.GetProperty("defaultGroups").EnumerateArray()).GetProperty("id").GetGuid());

        using var unknownGroup = await SendAdminAsync(HttpMethod.Put, $"/api/identity-providers/{providerId}", token, new { defaultGroupIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, unknownGroup.StatusCode);

        // Deleting the Group removes it from the default Groups.
        using var deleteGroup = await SendAdminAsync(HttpMethod.Delete, $"/api/groups/{groupId}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteGroup.StatusCode);
        using var afterDelete = await SendAdminAsync(HttpMethod.Get, $"/api/identity-providers/{providerId}", token);
        Assert.Empty((await afterDelete.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("defaultGroups").EnumerateArray());

        await MakeGooglePresetAsync(providerId);
        using var google = await SendAdminAsync(HttpMethod.Put, $"/api/identity-providers/{providerId}", token, new { allowedEmailDomains = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, google.StatusCode);
    }

    [Fact]
    public async Task EmailMatch_LinksOnlyAfterSigningInWithPasswordAndSecondFactor()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        await ConfigureSignUpAsync(providerId, allowSignUp: true, [], []);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        factory.SecondFactor.Enroll(userId, Code);

        // The Email of the provider differs in case only; the account is not linked, and no User is created.
        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-email-match", email.ToUpperInvariant()), flowCookie);

        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Null(ReadCookie(callback, SessionCookie.Name));
        var html = await ReadAsync(callback);
        Assert.Contains(ExternalCallbackModel.LinkRequiredMessage, html, StringComparison.Ordinal);
        Assert.Contains($"{SessionAuthenticationHandler.SignInPath}?returnUrl={Uri.EscapeDataString(ExternalLinkCompleteModel.PagePath)}", html, StringComparison.Ordinal);
        var linkCookie = $"{ExternalLinkCookie.Name}={ReadCookie(callback, ExternalLinkCookie.Name)}";
        Assert.Empty(await GetIdentitiesAsync(userId));
        Assert.Null(await FindUserByEmailAsync(email.ToUpperInvariant(), exceptId: userId));

        // The password alone does not link.
        using var signIn = await SignInAsync(email, Password, ExternalLinkCompleteModel.PagePath);
        var pendingToken = ReadCookie(signIn, PendingSignInCookie.Name);
        Assert.NotNull(pendingToken);
        Assert.Null(ReadCookie(signIn, SessionCookie.Name));
        Assert.Empty(await GetIdentitiesAsync(userId));

        using var secondFactor = await SubmitSecondFactorAsync(pendingToken, Code, ExternalLinkCompleteModel.PagePath);
        Assert.Equal(HttpStatusCode.Redirect, secondFactor.StatusCode);
        Assert.Equal(ExternalLinkCompleteModel.PagePath, secondFactor.Headers.Location?.OriginalString);
        var sessionToken = ReadCookie(secondFactor, SessionCookie.Name)!;
        Assert.Empty(await GetIdentitiesAsync(userId));

        using var complete = await GetAsync(ExternalLinkCompleteModel.PagePath, sessionToken, linkCookie);

        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        Assert.Equal(SecurityModel.PagePath, complete.Headers.Location?.OriginalString);
        Assert.True(IsDeleted(complete, ExternalLinkCookie.Name));

        var identity = Assert.Single(await GetIdentitiesAsync(userId));
        Assert.Equal("subject-email-match", identity.Subject);
        var linked = Assert.Single(await GetEventsAsync(AuditEventTypes.ExternalIdentities.Linked, userId.ToString()));
        Assert.Contains("via sign-in prompt", linked.Details, StringComparison.Ordinal);

        // The account now signs in, still with the second factor.
        var (nextUrl, nextCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var next = await CallbackAsync(slug, provider.Authorize(nextUrl, "subject-email-match", email), nextCookie);

        Assert.NotNull(ReadCookie(next, PendingSignInCookie.Name));
        Assert.Null(ReadCookie(next, SessionCookie.Name));
    }

    [Fact]
    public async Task EmailMatch_IsNotLinked_WithASessionFromBefore_OrOfAnotherUser()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        var earlierSession = await factory.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-1));

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-stale", email), flowCookie, earlierSession);
        var linkCookie = $"{ExternalLinkCookie.Name}={ReadCookie(callback, ExternalLinkCookie.Name)}";

        // A Session from before the account arrived must sign in again; the pending link is kept for that.
        using var stale = await GetAsync(ExternalLinkCompleteModel.PagePath, earlierSession, linkCookie);

        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        Assert.StartsWith(SessionAuthenticationHandler.SignInPath, stale.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.False(IsDeleted(stale, ExternalLinkCookie.Name));
        Assert.Empty(await GetIdentitiesAsync(userId));

        // Signing in to another User links nothing, and the pending link is gone.
        var (otherId, otherEmail) = await factory.CreateUserWithPasswordAsync(Password);
        using var otherSignIn = await SignInAsync(otherEmail, Password, ExternalLinkCompleteModel.PagePath);
        var otherSession = ReadCookie(otherSignIn, SessionCookie.Name)!;

        using var wrongUser = await GetAsync(ExternalLinkCompleteModel.PagePath, otherSession, linkCookie);

        Assert.Equal(HttpStatusCode.OK, wrongUser.StatusCode);
        Assert.Contains(ExternalLinkCompleteModel.WrongUserMessage, await ReadAsync(wrongUser), StringComparison.Ordinal);
        Assert.True(IsDeleted(wrongUser, ExternalLinkCookie.Name));
        Assert.Empty(await GetIdentitiesAsync(userId));
        Assert.Empty(await GetIdentitiesAsync(otherId));
    }

    [Fact]
    public async Task EmailMatch_Abandoned_LinksNothing()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-abandoned", email), flowCookie);
        Assert.NotNull(ReadCookie(callback, ExternalLinkCookie.Name));

        // The User signs in later without the cookie of the pending link, e.g. in another browser.
        using var signIn = await SignInAsync(email, Password, ExternalLinkCompleteModel.PagePath);
        var sessionToken = ReadCookie(signIn, SessionCookie.Name)!;
        using var complete = await GetAsync(ExternalLinkCompleteModel.PagePath, sessionToken, cookie: null);

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Contains(ExternalLinkCompleteModel.FailedMessage, await ReadAsync(complete), StringComparison.Ordinal);
        Assert.Empty(await GetIdentitiesAsync(userId));
    }

    [Fact]
    public async Task EmailMatch_WithAnUnverifiedEmail_IsRefusedLikeAnyOtherAccount()
    {
        var provider = factory.ExternalProviders.Create();
        var (_, slug) = await factory.CreateIdentityProviderAsync(provider);
        var (userId, email) = await factory.CreateUserWithPasswordAsync(Password);
        provider.ChangeClaims = claims => claims["email_verified"] = false;

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, "subject-unverified-match", email), flowCookie);

        Assert.Null(ReadCookie(callback, ExternalLinkCookie.Name));
        Assert.Contains(ExternalCallbackModel.GetNotPossibleMessage("Test IdP"), await ReadAsync(callback), StringComparison.Ordinal);
        Assert.Empty(await GetIdentitiesAsync(userId));
    }

    [Fact]
    public async Task UserWithoutPassword_CannotUsePasswordSignIn_ButCanSetAPassword()
    {
        var (userId, email, sessionToken, _) = await ProvisionAsync();

        using var passwordSignIn = await SignInAsync(email, Password, returnUrl: null);

        Assert.Equal(HttpStatusCode.OK, passwordSignIn.StatusCode);
        Assert.Contains(LoginModel.InvalidCredentialsMessage, await ReadAsync(passwordSignIn), StringComparison.Ordinal);
        var failure = (await GetEventsAsync(AuditEventTypes.Account.SignIn, userId.ToString())).Last();
        Assert.Equal(AuditOutcome.Failed, failure.Outcome);
        Assert.Equal("Wrong password", failure.Details);

        var security = await GetPageAsync(SecurityModel.PagePath, sessionToken);
        Assert.Contains("Set a password", security, StringComparison.Ordinal);

        using var page = await GetAsync(PasswordModel.PagePath, sessionToken, cookie: null);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await ReadAsync(page);
        Assert.Contains("Set a password", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"CurrentPassword\"", html, StringComparison.Ordinal);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, PasswordModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("NewPassword", Password),
                new("ConfirmPassword", Password),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");
        using var set = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Contains("Your password was set.", await ReadAsync(set), StringComparison.Ordinal);
        Assert.True((await GetUserByEmailAsync(email)).HasPassword);
        Assert.Contains(
            await GetEventsAsync(AuditEventTypes.Account.PasswordChanged, userId.ToString()),
            changed => changed.Details?.StartsWith("First password set", StringComparison.Ordinal) == true);

        using var signIn = await SignInAsync(email, Password, returnUrl: null);
        Assert.NotNull(ReadCookie(signIn, SessionCookie.Name));
    }

    [Fact]
    public async Task UserWithoutPassword_SettingAPassword_NeedsARecentAuthentication()
    {
        var (userId, _, _, _) = await ProvisionAsync();
        var oldSession = await factory.CreateSessionAsync(userId, DateTime.UtcNow.AddMinutes(-20), ["fed"]);

        using var page = await GetAsync(PasswordModel.PagePath, oldSession, cookie: null);

        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith(SessionAuthenticationHandler.SignInPath, page.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserWithoutPassword_CannotUnlinkTheirLastAccount()
    {
        var (userId, _, sessionToken, identityId) = await ProvisionAsync();

        using var page = await GetAsync(ExternalAccountsModel.PagePath, sessionToken, cookie: null);
        Assert.Contains("You have no password.", await ReadAsync(page), StringComparison.Ordinal);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ExternalAccountsModel.PagePath}?handler=Unlink")
        {
            Content = new FormUrlEncodedContent(
            [
                new("ExternalIdentityId", identityId.ToString()),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{SessionCookie.Name}={sessionToken}; {antiforgeryCookie}");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{ExternalAccountsModel.PagePath}?status=last-sign-in-method", response.Headers.Location?.OriginalString);
        Assert.Single(await GetIdentitiesAsync(userId));
        Assert.Empty(await GetEventsAsync(AuditEventTypes.ExternalIdentities.Unlinked, userId.ToString()));

        var error = await GetPageAsync($"{ExternalAccountsModel.PagePath}?status=last-sign-in-method", sessionToken);
        Assert.Contains(ExternalAccountsModel.LastSignInMethodMessage, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminApi_ListsAndRemovesLinkedAccounts_ButKeepsTheLastWayToSignIn()
    {
        var (userId, _, sessionToken, identityId) = await ProvisionAsync();
        var token = await factory.IssueAccessTokenAsync(await factory.CreateAdministratorAsync(), [AdminApiScope.Name]);

        using var user = await SendAdminAsync(HttpMethod.Get, $"/api/users/{userId}", token);
        Assert.False((await user.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("hasPassword").GetBoolean());

        using var list = await SendAdminAsync(HttpMethod.Get, $"/api/users/{userId}/external-identities", token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var identity = Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).EnumerateArray());
        Assert.Equal(identityId, identity.GetProperty("id").GetGuid());
        Assert.Equal("Test IdP", identity.GetProperty("providerName").GetString());
        Assert.False(identity.TryGetProperty("subject", out _));

        using var refused = await SendAdminAsync(HttpMethod.Delete, $"/api/users/{userId}/external-identities/{identityId}", token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        using var setPassword = await SendAdminAsync(HttpMethod.Put, $"/api/users/{userId}/password", token, new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, setPassword.StatusCode);

        using var removed = await SendAdminAsync(HttpMethod.Delete, $"/api/users/{userId}/external-identities/{identityId}", token);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(await GetIdentitiesAsync(userId));
        var unlinked = Assert.Single(await GetEventsAsync(AuditEventTypes.ExternalIdentities.Unlinked, userId.ToString()));
        Assert.Contains("by an administrator", unlinked.Details, StringComparison.Ordinal);

        using var missing = await SendAdminAsync(HttpMethod.Delete, $"/api/users/{userId}/external-identities/{identityId}", token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AdminApi_LinkedAccounts_NeedThePermissions()
    {
        var (userId, _, _, identityId) = await ProvisionAsync();
        var readerToken = await factory.IssueAccessTokenAsync(await factory.CreateUserAsync(PermissionNames.Users.Read), [AdminApiScope.Name]);

        using var list = await SendAdminAsync(HttpMethod.Get, $"/api/users/{userId}/external-identities", readerToken);
        using var remove = await SendAdminAsync(HttpMethod.Delete, $"/api/users/{userId}/external-identities/{identityId}", readerToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
    }

    // Signs up a new User through a provider that allows it; returns the User, its Session and its link.
    private async Task<(Guid UserId, string Email, string SessionToken, Guid IdentityId)> ProvisionAsync()
    {
        var provider = factory.ExternalProviders.Create();
        var (providerId, slug) = await factory.CreateIdentityProviderAsync(provider);
        await ConfigureSignUpAsync(providerId, allowSignUp: true, [], []);
        var email = $"{Guid.NewGuid():N}@example.com";
        var subject = $"subject-{Guid.NewGuid():N}";

        var (authorizationUrl, flowCookie) = await StartAsync(slug, SecurityModel.PagePath);
        using var callback = await CallbackAsync(slug, provider.Authorize(authorizationUrl, subject, email), flowCookie);

        var sessionToken = ReadCookie(callback, SessionCookie.Name) ?? throw new InvalidOperationException("The User was not signed up.");
        var user = await GetUserByEmailAsync(email);
        var identity = Assert.Single(await GetIdentitiesAsync(user.Id));

        return (user.Id, email, sessionToken, identity.Id);
    }

    private async Task ConfigureSignUpAsync(Guid providerId, bool allowSignUp, IReadOnlyList<string> domains, IReadOnlyList<Guid> groupIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var provider = await context.IdentityProviders.Include(provider => provider.DefaultGroups)
            .SingleAsync(provider => provider.Id == providerId, TestContext.Current.CancellationToken);
        var groups = await context.Groups.Where(group => groupIds.Contains(group.Id)).ToListAsync(TestContext.Current.CancellationToken);

        provider.ConfigureSignUp(allowSignUp, domains);
        provider.SetDefaultGroups(groups);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The Google preset fixes the Issuer, so a provider of the fake becomes one afterwards.
    private async Task MakeGooglePresetAsync(Guid providerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        await context.Database.ExecuteSqlAsync(
            $"UPDATE identity.identity_providers SET \"Preset\" = {(int)IdentityProviderPreset.Google} WHERE \"Id\" = {providerId}",
            TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CreateGroupAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        var group = Group.Create($"group-{Guid.NewGuid():N}"[..20], "Employees");
        context.Groups.Add(group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return group.Id;
    }

    private async Task<(Uri AuthorizationUrl, string FlowCookie)> StartAsync(string slug, string returnUrl)
    {
        using var response = await GetAsync($"/account/external/{slug}?returnUrl={Uri.EscapeDataString(returnUrl)}", sessionToken: null, cookie: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return (response.Headers.Location!, $"{ExternalSignInCookie.Name}={ReadCookie(response, ExternalSignInCookie.Name)}");
    }

    private async Task<HttpResponseMessage> CallbackAsync(string slug, Dictionary<string, string> parameters, string flowCookie, string? sessionToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString($"/account/external/{slug}/callback", parameters!));

        request.Headers.Add("Cookie", sessionToken is null ? flowCookie : $"{flowCookie}; {SessionCookie.Name}={sessionToken}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password, string? returnUrl)
    {
        using var page = await GetAsync(SessionAuthenticationHandler.SignInPath, sessionToken: null, cookie: null);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        var form = new List<KeyValuePair<string, string>>
        {
            new("Email", email),
            new("Password", password),
            new("__RequestVerificationToken", antiforgeryToken)
        };

        if (returnUrl is not null)
        {
            form.Add(new("ReturnUrl", returnUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SessionAuthenticationHandler.SignInPath) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("Cookie", antiforgeryCookie);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SubmitSecondFactorAsync(string pendingToken, string code, string returnUrl)
    {
        using var page = await GetAsync(SessionAuthenticationHandler.SignInPath, sessionToken: null, cookie: null);
        var (antiforgeryToken, antiforgeryCookie) = await AuthorizationCodeFlow.ReadAntiforgeryAsync(page);

        using var request = new HttpRequestMessage(HttpMethod.Post, SecondFactorModel.PagePath)
        {
            Content = new FormUrlEncodedContent(
            [
                new("Method", FakeSecondFactor.MethodName),
                new("Code", code),
                new("ReturnUrl", returnUrl),
                new("__RequestVerificationToken", antiforgeryToken)
            ])
        };
        request.Headers.Add("Cookie", $"{PendingSignInCookie.Name}={pendingToken}; {antiforgeryCookie}");

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAdminAsync(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? sessionToken, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var cookies = new List<string>();

        if (sessionToken is not null)
        {
            cookies.Add($"{SessionCookie.Name}={sessionToken}");
        }

        if (cookie is not null)
        {
            cookies.Add(cookie);
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> GetPageAsync(string path, string sessionToken)
    {
        using var response = await GetAsync(path, sessionToken, cookie: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync(response);
    }

    private static Task<string> ReadAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

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

    private static bool IsDeleted(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith($"{name}=;", StringComparison.Ordinal));
    }

    private async Task<User> GetUserByEmailAsync(string email)
    {
        return await FindUserByEmailAsync(email, exceptId: null) ?? throw new InvalidOperationException("No User has this Email.");
    }

    private async Task<User?> FindUserByEmailAsync(string email, Guid? exceptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();
        var normalized = User.Normalize(email);

        return await context.Users.AsNoTracking()
            .Include(user => user.Groups)
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalized && user.Id != exceptId, TestContext.Current.CancellationToken);
    }

    private async Task<bool> UserExistsAsync(string email) => await FindUserByEmailAsync(email, exceptId: null) is not null;

    private async Task<List<Session>> GetSessionsAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.Sessions.AsNoTracking()
            .Where(session => session.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<ExternalIdentity>> GetIdentitiesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.ExternalIdentities.AsNoTracking()
            .Where(identity => identity.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditEvent>> GetEventsAsync(string type, string targetId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthoraContext>();

        return await context.AuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.Type == type && auditEvent.TargetId == targetId)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
