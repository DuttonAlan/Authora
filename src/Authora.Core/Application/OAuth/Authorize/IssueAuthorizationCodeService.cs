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
/// <param name="secretGenerator">The generator for the random code.</param>
/// <param name="secretHasher">The hasher for the stored code.</param>
/// <param name="timeProvider">The TimeProvider for the creation time and expiry.</param>
/// <param name="options">The OAuth settings with the lifetime of the code.</param>
/// <param name="logger">The Logger to record the issued code.</param>
public sealed class IssueAuthorizationCodeService(
    IAuthorizationCodeRepository authorizationCodeRepository,
    IUserRepository userRepository,
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
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The plain-text code, which is only sent to the Client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the User does not exist or cannot sign in.</exception>
    public async Task<string> ExecuteAsync(
        ValidatedAuthorizationRequest request,
        Guid userId,
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
            request.RequestedRedirectUri,
            request.Scopes.Select(scope => scope.Name),
            request.CodeChallenge,
            timeProvider.GetUtcNow().UtcDateTime,
            options.Value.AuthorizationCodeLifetime);

        await authorizationCodeRepository.CreateAuthorizationCode(authorizationCode, cancellationToken);

        logger.AuthorizationCodeIssued(authorizationCode.Id, request.Client.Id, user.Id, authorizationCode.Scope);

        return code;
    }
}
