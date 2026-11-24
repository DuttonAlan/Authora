// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The Service to issue an Authorization Code after the User approved an authorization request.
/// </summary>
/// <param name="authorizationCodeRepository">An Instance of a IAuthorizationCodeRepository to store the code.</param>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="consentRepository">An Instance of a IConsentRepository to bind the code to the Consent of the User.</param>
/// <param name="secretGenerator">The generator for the random code.</param>
/// <param name="secretHasher">The hasher for the stored code.</param>
/// <param name="timeProvider">The TimeProvider for the creation time and expiry.</param>
/// <param name="options">The OAuth settings with the lifetime of the code.</param>
/// <param name="logger">The Logger to record the issued code.</param>
public sealed class IssueAuthorizationCodeService(
    IAuthorizationCodeRepository authorizationCodeRepository,
    IUserRepository userRepository,
    IConsentRepository consentRepository,
    ISecretGenerator secretGenerator,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    IOptions<OAuthOptions> options,
    ILogger<IssueAuthorizationCodeService> logger)
{
    /// <summary>
    ///     Issues a new Authorization Code for an approved request.
    /// </summary>
    /// <param name="request">The validated request the User approved.</param>
    /// <param name="userId">The Id of the signed-in User.</param>
    /// <param name="sessionId">The Id of the Session of the User. The code can only be exchanged while it is active.</param>
    /// <param name="authenticatedAt">When the User last authenticated in the Session (UTC), for <c>auth_time</c> in the ID Token.</param>
    /// <param name="authenticationMethods">The authentication methods the User completed in the Session, e.g. <c>pwd</c>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The plain-text code, which is only sent to the Client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the User does not exist or cannot sign in.</exception>
    public async Task<string> ExecuteAsync(
        ValidatedAuthorizationRequest request,
        Guid userId,
        Guid sessionId,
        DateTime authenticatedAt,
        IReadOnlyCollection<string> authenticationMethods,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The Session was validated before, so this only fails if the User changed in the meantime.
        var user = await userRepository.GetUserById(userId, cancellationToken);

        if (user is null || !user.CanSignIn)
        {
            throw new InvalidOperationException("The signed-in User can no longer sign in.");
        }

        var code = secretGenerator.Generate();

        var authorizationCode = AuthorizationCode.Create(
            secretHasher.Hash(code),
            request.Client,
            user,
            sessionId,
            request.RequestedRedirectUri,
            request.Scopes.Select(scope => scope.Name),
            request.Resources,
            request.CodeChallenge,
            request.Nonce,
            authenticatedAt,
            authenticationMethods,
            timeProvider.GetUtcNow().UtcDateTime,
            options.Value.AuthorizationCodeLifetime);

        // A third-party Client gets the code only under a Consent, so the code is bound to it. Without one (revoked in
        // the meantime), the code stays unbound and the token endpoint rejects it.
        if (!request.Client.IsFirstParty
            && await consentRepository.GetConsent(user.Id, request.Client.Id, cancellationToken) is { } consent)
        {
            authorizationCode.BindToConsent(consent);
        }

        await authorizationCodeRepository.CreateAuthorizationCode(authorizationCode, cancellationToken);

        logger.AuthorizationCodeIssued(authorizationCode.Id, request.Client.Id, user.Id, authorizationCode.Scope);

        return code;
    }
}
