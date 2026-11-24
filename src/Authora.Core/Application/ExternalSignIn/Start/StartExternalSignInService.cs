// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.OAuth;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.ExternalSignIn.Start;

/// <summary>
///     The Service to start a sign-in through an external identity provider, or to link an account there to the
///     signed-in User: creates <c>state</c>, <c>nonce</c> and a PKCE code verifier, and the authorization request.
/// </summary>
/// <remarks>
///     Nothing is stored: the caller keeps the returned <see cref="ExternalSignInFlow"/> in an encrypted, short-lived
///     cookie of the browser, which <see cref="Complete.CompleteExternalSignInService"/> receives with the response.
/// </remarks>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the provider.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to check existing links.</param>
/// <param name="userRepository">An Instance of a IUserRepository to check the User of a link.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to check the Session of a link.</param>
/// <param name="relyingParty">Creates the authorization request.</param>
/// <param name="secretGenerator">The generator for state, nonce and code verifier.</param>
/// <param name="timeProvider">The TimeProvider for the Session checks.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer of Authora, for the Redirect URI.</param>
/// <param name="signInOptions">The sign-in settings with the idle timeout and the recent authentication window.</param>
/// <param name="logger">The Logger to record the start.</param>
public sealed class StartExternalSignInService(
    IIdentityProviderRepository providerRepository,
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IOidcRelyingParty relyingParty,
    ISecretGenerator secretGenerator,
    TimeProvider timeProvider,
    IOptions<OAuthOptions> oauthOptions,
    IOptions<SignInOptions> signInOptions,
    ILogger<StartExternalSignInService> logger)
{
    /// <summary>
    ///     Starts the flow.
    /// </summary>
    /// <param name="request">The provider, where to continue afterwards and, for a link, the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The URL to send the browser to and the flow to keep, or why the flow cannot start.</returns>
    public async Task<StartExternalSignInResult> ExecuteAsync(
        StartExternalSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = await providerRepository.GetProviderBySlug(request.Slug, cancellationToken);

        if (provider is null || !provider.IsEnabled)
        {
            return StartExternalSignInResult.UnknownProvider;
        }

        if (request.Link is { } link)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var user = await userRepository.GetUserById(link.UserId, cancellationToken);
            var session = await sessionRepository.GetSessionById(link.SessionId, cancellationToken);
            var options = signInOptions.Value;

            // Checked again, although the page only runs with a recent Session: it may have ended since.
            if (user is null
                || !user.CanSignIn
                || session is null
                || session.UserId != user.Id
                || session.SecurityStamp != user.SecurityStamp
                || !session.IsActive(now, options.SessionIdleTimeout)
                || now - session.AuthenticatedAt > options.RecentAuthenticationWindow)
            {
                return StartExternalSignInResult.SessionEnded;
            }

            var identities = await externalIdentityRepository.GetIdentitiesOfUser(user.Id, cancellationToken);

            if (identities.Any(identity => identity.ProviderId == provider.Id))
            {
                return StartExternalSignInResult.AlreadyLinked;
            }
        }

        var flow = new ExternalSignInFlow(
            provider.Id,
            provider.Slug,
            secretGenerator.Generate(),
            secretGenerator.Generate(),
            secretGenerator.Generate(),
            request.ReturnUrl,
            request.Link?.SessionId);

        Uri authorizationUrl;

        try
        {
            authorizationUrl = await relyingParty.CreateAuthorizationUrlAsync(
                new ExternalAuthorizationRequest(
                    provider.Issuer,
                    provider.ClientId,
                    provider.Scopes,
                    ExternalSignInOptions.GetRedirectUri(oauthOptions.Value.Issuer, provider.Slug),
                    flow.State,
                    flow.Nonce,
                    Pkce.ComputeChallenge(flow.CodeVerifier)),
                cancellationToken);
        }
        catch (ExternalSignInException exception)
        {
            logger.ExternalSignInNotStarted(provider.Slug, exception.Message);

            return StartExternalSignInResult.ProviderUnavailable(provider.Name);
        }

        if (request.Link is { } started)
        {
            logger.ExternalLinkStarted(started.UserId, provider.Slug);
        }
        else
        {
            logger.ExternalSignInStarted(provider.Slug);
        }

        return new StartExternalSignInResult(StartExternalSignInStatus.Started, provider.Name, authorizationUrl, flow);
    }
}

/// <summary>
///     What starts a flow through an external identity provider.
/// </summary>
/// <param name="Slug">The Slug of the provider.</param>
/// <param name="ReturnUrl">The local URL to continue with afterwards.</param>
/// <param name="Link">The signed-in User and their Session for a link, or null for a sign-in.</param>
public sealed record StartExternalSignInRequest(string Slug, string ReturnUrl, ExternalLinkRequest? Link = null);

/// <summary>
///     The signed-in User who links an account at a provider.
/// </summary>
/// <param name="UserId">Id of the User.</param>
/// <param name="SessionId">Id of the Session of the browser.</param>
public sealed record ExternalLinkRequest(Guid UserId, Guid SessionId);

/// <summary>
///     The outcome of <see cref="StartExternalSignInService"/>.
/// </summary>
public enum StartExternalSignInStatus
{
    /// <summary>
    ///     The flow started; the browser is sent to the provider.
    /// </summary>
    Started,

    /// <summary>
    ///     No enabled provider has the Slug.
    /// </summary>
    UnknownProvider,

    /// <summary>
    ///     The discovery document of the provider could not be fetched.
    /// </summary>
    ProviderUnavailable,

    /// <summary>
    ///     For a link: the Session ended or the User did not authenticate recently.
    /// </summary>
    SessionEnded,

    /// <summary>
    ///     For a link: the User already has an account at the provider linked.
    /// </summary>
    AlreadyLinked
}

/// <summary>
///     The result of <see cref="StartExternalSignInService"/>.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="ProviderName">The Name of the provider, or null if it is unknown.</param>
/// <param name="AuthorizationUrl">The URL of the authorization request, if the flow started.</param>
/// <param name="Flow">The flow to keep in the browser, if it started.</param>
public sealed record StartExternalSignInResult(
    StartExternalSignInStatus Status,
    string? ProviderName = null,
    Uri? AuthorizationUrl = null,
    ExternalSignInFlow? Flow = null)
{
    /// <summary>
    ///     The result for an unknown or disabled provider.
    /// </summary>
    public static readonly StartExternalSignInResult UnknownProvider = new(StartExternalSignInStatus.UnknownProvider);

    /// <summary>
    ///     The result for a link whose Session ended.
    /// </summary>
    public static readonly StartExternalSignInResult SessionEnded = new(StartExternalSignInStatus.SessionEnded);

    /// <summary>
    ///     The result for a link to a provider the User is already linked to.
    /// </summary>
    public static readonly StartExternalSignInResult AlreadyLinked = new(StartExternalSignInStatus.AlreadyLinked);

    /// <summary>
    ///     Creates the result for a provider whose discovery document could not be fetched.
    /// </summary>
    /// <param name="providerName">The Name of the provider.</param>
    /// <returns>The result.</returns>
    public static StartExternalSignInResult ProviderUnavailable(string providerName) => new(StartExternalSignInStatus.ProviderUnavailable, providerName);
}
