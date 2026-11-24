// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Text;

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Mfa;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.ExternalSignIn.Complete;

/// <summary>
///     The Service that handles the authorization response of an external identity provider: checks it against the
///     flow of the browser, redeems the code, verifies the ID Token, and then signs the linked User in, or links the
///     account to the signed-in User.
/// </summary>
/// <remarks>
///     <para>
///         The sign-in follows the rules of a password sign-in: the User must be able to sign in and not be locked out,
///         and a User who enrolled a second factor gets only a pending sign-in, which <c>CompleteSignInService</c> turns
///         into a Session. The provider counts as the first factor (<c>fed</c>); it does not replace a second one.
///     </para>
///     <para>
///         Every failure is recorded as <see cref="AuditEventTypes.Account.ExternalSignInFailed"/> with the reason,
///         while the User only sees that the sign-in failed.
///     </para>
/// </remarks>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the provider.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to find and create links.</param>
/// <param name="userRepository">An Instance of a IUserRepository to load and save the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to create the Session or check the Session of a link.</param>
/// <param name="challengeRepository">An Instance of a ISignInChallengeRepository to create a pending sign-in.</param>
/// <param name="secondFactors">The second factors, to check whether the User must complete one.</param>
/// <param name="relyingParty">Redeems the code and verifies the ID Token.</param>
/// <param name="secretProtector">Decrypts the Client Secret of the provider.</param>
/// <param name="secretGenerator">The generator for the Session token and the token of a pending sign-in.</param>
/// <param name="secretHasher">The hasher for the stored tokens.</param>
/// <param name="administratorMfaPolicy">Decides whether a User without a second factor must set one up right away.</param>
/// <param name="timeProvider">The TimeProvider for the lockout, the Session and the link.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer of Authora, for the Redirect URI.</param>
/// <param name="signInOptions">The sign-in settings.</param>
/// <param name="auditLog">The audit log to record sign-ins, links and failures.</param>
/// <param name="logger">The Logger to record sign-ins, links and failures.</param>
public sealed class CompleteExternalSignInService(
    IIdentityProviderRepository providerRepository,
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    ISignInChallengeRepository challengeRepository,
    SecondFactorRegistry secondFactors,
    IOidcRelyingParty relyingParty,
    IProviderSecretProtector secretProtector,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    AdministratorMfaPolicy administratorMfaPolicy,
    TimeProvider timeProvider,
    IOptions<OAuthOptions> oauthOptions,
    IOptions<SignInOptions> signInOptions,
    IAuditLog auditLog,
    ILogger<CompleteExternalSignInService> logger)
{
    // Longest error code of a provider that is recorded; longer values are cut.
    private const int MaxErrorLength = 64;

    /// <summary>
    ///     Handles the authorization response.
    /// </summary>
    /// <param name="request">The response and the flow of the browser.</param>
    /// <param name="currentSessionId">Id of the Session of the browser, or null. A link must arrive in its Session.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The outcome, with the tokens for the cookies of a sign-in.</returns>
    public async Task<ExternalSignInResult> ExecuteAsync(
        CompleteExternalSignInRequest request,
        Guid? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var flow = request.Flow;

        if (flow is null || !string.Equals(flow.Slug, request.Slug, StringComparison.Ordinal))
        {
            var unknown = await providerRepository.GetProviderBySlug(request.Slug, cancellationToken);

            return await FailAsync(unknown, request.Slug, null, null, "No sign-in through the provider is running in this browser, or it expired", cancellationToken);
        }

        var provider = await providerRepository.GetProviderById(flow.ProviderId, cancellationToken);

        if (provider is null || !provider.IsEnabled || !string.Equals(provider.Slug, flow.Slug, StringComparison.Ordinal))
        {
            return await FailAsync(provider, flow.Slug, flow, null, "The provider was disabled or deleted", cancellationToken);
        }

        if (!StateMatches(flow.State, request.State))
        {
            return await FailAsync(provider, provider.Slug, flow, null, "The state does not match", cancellationToken);
        }

        // RFC 9207: a provider that names itself must name the expected Issuer, so a response from another provider
        // (mix-up attack) is not redeemed here.
        if (request.Issuer is not null && !string.Equals(request.Issuer, provider.Issuer, StringComparison.Ordinal))
        {
            return await FailAsync(provider, provider.Slug, flow, null, "The iss parameter names another Issuer", cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.Error))
        {
            return await FailAsync(provider, provider.Slug, flow, null, $"The provider returned the error '{Sanitize(request.Error)}'", cancellationToken);
        }

        if (string.IsNullOrEmpty(request.Code))
        {
            return await FailAsync(provider, provider.Slug, flow, null, "The response contains no code", cancellationToken);
        }

        string? clientSecret = null;

        if (provider.ProtectedClientSecret is { } protectedSecret)
        {
            clientSecret = secretProtector.Unprotect(protectedSecret);

            if (clientSecret is null)
            {
                return await FailAsync(provider, provider.Slug, flow, null, "The Client Secret cannot be decrypted; set it again", cancellationToken);
            }
        }

        ExternalIdentityClaims claims;

        try
        {
            claims = await relyingParty.RedeemCodeAsync(
                new ExternalCodeRedemption(
                    provider.Issuer,
                    provider.ClientId,
                    clientSecret,
                    request.Code,
                    ExternalSignInOptions.GetRedirectUri(oauthOptions.Value.Issuer, provider.Slug),
                    flow.CodeVerifier,
                    flow.Nonce),
                cancellationToken);
        }
        catch (ExternalSignInException exception)
        {
            return await FailAsync(provider, provider.Slug, flow, null, exception.Message, cancellationToken);
        }

        return flow.IsLink
            ? await LinkAsync(provider, flow, claims, currentSessionId, cancellationToken)
            : await SignInAsync(provider, flow, claims, cancellationToken);
    }

    private async Task<ExternalSignInResult> SignInAsync(
        IdentityProvider provider,
        ExternalSignInFlow flow,
        ExternalIdentityClaims claims,
        CancellationToken cancellationToken)
    {
        var identity = await externalIdentityRepository.FindIdentity(provider.Id, claims.Subject, cancellationToken);

        if (identity is null)
        {
            logger.ExternalIdentityNotLinked(provider.Slug);
            await RecordFailureAsync(provider, null, "The account at the provider is not linked to a User", cancellationToken);

            return Result(ExternalSignInStatus.NotLinked, provider, flow);
        }

        var user = await userRepository.GetUserById(identity.UserId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (user is null || !user.CanSignIn)
        {
            return await FailAsync(provider, provider.Slug, flow, identity.UserId, "The User cannot sign in", cancellationToken);
        }

        if (user.IsLockedOut(now))
        {
            return await FailAsync(provider, provider.Slug, flow, user.Id, "The User is locked out", cancellationToken);
        }

        identity.RecordUse(now);

        if ((await secondFactors.GetEnrolledAsync(user.Id, cancellationToken)).Count > 0)
        {
            return await StartPendingSignInAsync(provider, flow, user, now, cancellationToken);
        }

        user.RecordSuccessfulSignIn(now);

        try
        {
            // Saves the User and the use of the identity; throws a ConflictException if the User was changed since it
            // was loaded, e.g. by a password reset.
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            return await FailAsync(provider, provider.Slug, flow, user.Id, "The User was changed by another request at the same time", cancellationToken);
        }

        var sessionToken = secretGenerator.Generate();
        var session = Session.Create(
            user,
            secretHasher.Hash(sessionToken),
            [AuthenticationMethods.Federated],
            now,
            signInOptions.Value.SessionLifetime);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.SignIn,
            user.Id,
            $"Session {session.Id} ({session.AuthenticationMethod}), identity provider '{provider.Slug}' ({provider.Id})"));

        await sessionRepository.CreateSession(session, cancellationToken);

        logger.ExternalSignedIn(user.Id, provider.Slug, session.Id);

        var mustEnroll = await administratorMfaPolicy.IsRequiredAsync(user.Id, cancellationToken);

        if (mustEnroll)
        {
            logger.SecondFactorEnrollmentRequired(user.Id);
        }

        return Result(ExternalSignInStatus.SignedIn, provider, flow) with
        {
            SessionToken = sessionToken,
            MustEnrollSecondFactor = mustEnroll
        };
    }

    // The provider is the first factor; like after a correct password, the failed attempts are not reset until the
    // second factor is complete.
    private async Task<ExternalSignInResult> StartPendingSignInAsync(
        IdentityProvider provider,
        ExternalSignInFlow flow,
        User user,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var challengeToken = secretGenerator.Generate();
        var challenge = SignInChallenge.Create(
            secretHasher.Hash(challengeToken),
            user,
            [AuthenticationMethods.Federated],
            now,
            signInOptions.Value.PendingSignInLifetime);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.SignInPending,
            user.Id,
            $"Pending sign-in {challenge.Id}, identity provider '{provider.Slug}' ({provider.Id})"));

        // Also saves the use of the identity.
        await challengeRepository.CreateChallenge(challenge, cancellationToken);

        logger.ExternalSignInPending(user.Id, provider.Slug, challenge.Id);

        return Result(ExternalSignInStatus.Pending, provider, flow) with { ChallengeToken = challengeToken };
    }

    private async Task<ExternalSignInResult> LinkAsync(
        IdentityProvider provider,
        ExternalSignInFlow flow,
        ExternalIdentityClaims claims,
        Guid? currentSessionId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = flow.LinkSessionId == currentSessionId && currentSessionId is { } sessionId
            ? await sessionRepository.GetSessionById(sessionId, cancellationToken)
            : null;
        var user = session is null ? null : await userRepository.GetUserById(session.UserId, cancellationToken);

        // The link was started in this Session, which must still be the Session of the browser and valid.
        if (session is null
            || user is null
            || !user.CanSignIn
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, signInOptions.Value.SessionIdleTimeout))
        {
            return await FailAsync(provider, provider.Slug, flow, session?.UserId, "The Session that started the link ended or is not the Session of the browser", cancellationToken);
        }

        var existing = await externalIdentityRepository.FindIdentity(provider.Id, claims.Subject, cancellationToken);

        if (existing is not null)
        {
            if (existing.UserId == user.Id)
            {
                return Result(ExternalSignInStatus.AlreadyLinked, provider, flow);
            }

            await RecordFailureAsync(provider, user.Id, "The account at the provider is already linked to another User", cancellationToken);
            logger.ExternalSignInFailed(provider.Slug, "the account at the provider is already linked to another User");

            return Result(ExternalSignInStatus.LinkConflict, provider, flow);
        }

        var identity = ExternalIdentity.Create(user.Id, provider.Id, claims.Subject, claims.Email, now);

        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.ExternalIdentities.Linked,
            user.Id,
            $"Identity {identity.Id}, identity provider '{provider.Slug}' ({provider.Id})"));

        // Fails if the User already has an account at the provider, or another request linked the same account.
        if (!await externalIdentityRepository.TryCreateIdentity(identity, cancellationToken))
        {
            await RecordFailureAsync(provider, user.Id, "The User already has an account at the provider linked, or the account was linked at the same time", cancellationToken);
            logger.ExternalSignInFailed(provider.Slug, "the link conflicts with an existing one");

            return Result(ExternalSignInStatus.LinkConflict, provider, flow);
        }

        logger.ExternalIdentityLinked(user.Id, identity.Id, provider.Slug);

        return Result(ExternalSignInStatus.Linked, provider, flow);
    }

    private async Task<ExternalSignInResult> FailAsync(
        IdentityProvider? provider,
        string slug,
        ExternalSignInFlow? flow,
        Guid? userId,
        string reason,
        CancellationToken cancellationToken)
    {
        logger.ExternalSignInFailed(slug, reason);
        await RecordFailureAsync(provider, userId, reason, cancellationToken);

        return new ExternalSignInResult(ExternalSignInStatus.Failed, provider?.Name, flow?.ReturnUrl);
    }

    private Task RecordFailureAsync(IdentityProvider? provider, Guid? userId, string reason, CancellationToken cancellationToken)
    {
        var details = provider is null
            ? reason
            : $"Identity provider '{provider.Slug}' ({provider.Id}): {reason}";

        var entry = userId is { } id
            ? AuditEntry.ForUser(AuditEventTypes.Account.ExternalSignInFailed, id, details)
            : provider is null
                ? new AuditEntry(AuditEventTypes.Account.ExternalSignInFailed, Details: details)
                : AuditEntry.ForIdentityProvider(AuditEventTypes.Account.ExternalSignInFailed, provider.Id, details);

        return auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }

    private static ExternalSignInResult Result(ExternalSignInStatus status, IdentityProvider provider, ExternalSignInFlow flow)
    {
        return new ExternalSignInResult(status, provider.Name, flow.ReturnUrl);
    }

    private static bool StateMatches(string expected, string? actual)
    {
        return actual is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    // The error code comes from the browser, so only harmless characters reach the logs and the audit log.
    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(Math.Min(value.Length, MaxErrorLength));

        foreach (var character in value.Take(MaxErrorLength))
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '?');
        }

        return builder.ToString();
    }
}
