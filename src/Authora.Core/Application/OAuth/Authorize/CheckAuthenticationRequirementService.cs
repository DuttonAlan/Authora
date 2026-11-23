// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Mfa;
using Authora.Core.Domain;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The Service to check whether the Session of a signed-in User is strong enough for an authorization request,
///     or whether the User must first complete another factor (step-up) or set one up.
/// </summary>
/// <remarks>
///     <para>
///         Multi-factor authentication is <b>required</b> if the Client requires it
///         (<see cref="Domain.Entities.Client.RequiresMfa"/>), or if the request contains the <c>authora.admin</c>
///         Scope and the User holds a Permission while <see cref="MfaOptions.RequireForAdministrators"/> is on. A User
///         without a second factor must then set one up.
///     </para>
///     <para>
///         <c>acr_values</c> is <b>voluntary</b> (OpenID Connect Core 1.0, Section 3.1.2.1): the User is asked for
///         a missing factor only if they enrolled one that reaches a requested level, or at least gets them closer to
///         it. Otherwise the Client gets the strongest level the User can reach and sees it in <c>acr</c>.
///     </para>
/// </remarks>
/// <param name="secondFactors">The second factors, to find those the User enrolled.</param>
/// <param name="administratorMfaPolicy">Decides whether the User must use MFA because they hold a Permission.</param>
/// <param name="logger">The Logger to record why the User is asked for more.</param>
public sealed class CheckAuthenticationRequirementService(
    SecondFactorRegistry secondFactors,
    AdministratorMfaPolicy administratorMfaPolicy,
    ILogger<CheckAuthenticationRequirementService> logger)
{
    /// <summary>
    ///     Checks the Session against the request.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="authenticationMethods">The methods the User completed in the Session, e.g. <c>pwd</c>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the Session is enough, or what the User must do first.</returns>
    public async Task<AuthenticationRequirement> ExecuteAsync(
        ValidatedAuthorizationRequest request,
        Guid userId,
        IReadOnlyCollection<string> authenticationMethods,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authenticationMethods);

        var current = AuthenticationContextClasses.Rank(AuthenticationContextClasses.Of(authenticationMethods));
        var requested = request.RequestedAcr is { } acr ? AuthenticationContextClasses.Rank(acr) : -1;
        var multiFactor = AuthenticationContextClasses.Rank(AuthenticationContextClasses.MultiFactor);

        var mfaRequired = current < multiFactor
            && (request.Client.RequiresMfa || (RequestsAdminApi(request) && await administratorMfaPolicy.IsRequiredAsync(userId, cancellationToken)));

        if (!mfaRequired && current >= requested)
        {
            return AuthenticationRequirement.Satisfied;
        }

        var missing = (await secondFactors.GetEnrolledAsync(userId, cancellationToken))
            .Where(factor => !SecondFactorRegistry.IsCompleted(factor, authenticationMethods))
            .Select(factor => factor.Method)
            .ToList();

        if (mfaRequired)
        {
            // Every second factor added to the password makes it a multi-factor sign-in.
            var requirement = missing.Count > 0 ? AuthenticationRequirement.StepUp : AuthenticationRequirement.Enrollment;
            logger.MfaRequired(request.Client.Id, userId, requirement);

            return requirement;
        }

        // Voluntary: aim for the requested level, or the strongest the User can reach with their factors.
        var reachable = AuthenticationContextClasses.Rank(AuthenticationContextClasses.Of([.. authenticationMethods, .. missing]));

        if (current < Math.Min(requested, reachable))
        {
            logger.AcrStepUpRequested(request.Client.Id, userId, request.RequestedAcr!);

            return AuthenticationRequirement.StepUp;
        }

        return AuthenticationRequirement.Satisfied;
    }

    private static bool RequestsAdminApi(ValidatedAuthorizationRequest request)
    {
        return request.Scopes.Any(scope => string.Equals(scope.Name, AdminApiScope.Name, StringComparison.Ordinal));
    }
}

/// <summary>
///     The result of <see cref="CheckAuthenticationRequirementService"/>.
/// </summary>
public enum AuthenticationRequirement
{
    /// <summary>
    ///     The Session is strong enough; the Client can get a code.
    /// </summary>
    Satisfied,

    /// <summary>
    ///     The User must first complete a factor they enrolled but the Session lacks.
    /// </summary>
    StepUp,

    /// <summary>
    ///     Multi-factor authentication is required, but the User has no second factor: they must set one up first.
    /// </summary>
    Enrollment
}
