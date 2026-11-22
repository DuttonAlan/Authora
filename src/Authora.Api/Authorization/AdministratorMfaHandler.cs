// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Mfa;
using Authora.Core.Domain;

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Checks an <see cref="AdministratorMfaRequirement"/> against the authentication methods the Access Token carries
///     from its Grant.
/// </summary>
/// <remarks>
///     Applies to every Access Token with the admin Scope, whichever Client it was issued to. A User who holds no
///     Permission passes, because there is nothing the token could do. A failed check is marked on the request, so
///     <see cref="AccessTokenAuthenticationHandler"/> answers with <c>insufficient_user_authentication</c> (RFC 9470).
/// </remarks>
/// <param name="policy">Decides whether the User must use multi-factor authentication.</param>
public sealed class AdministratorMfaHandler(AdministratorMfaPolicy policy) : AuthorizationHandler<AdministratorMfaRequirement>
{
    /// <summary>
    ///     Key of <see cref="HttpContext.Items"/> that marks a request rejected for lacking multi-factor authentication.
    /// </summary>
    public const string InsufficientAuthenticationItem = "Authora.InsufficientUserAuthentication";

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministratorMfaRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!policy.IsEnabled)
        {
            context.Succeed(requirement);

            return;
        }

        var methods = context.User.FindAll(AccessTokenClaimTypes.AuthenticationMethod).Select(claim => claim.Value).ToList();

        if (AuthenticationContextClasses.Satisfies(methods, AuthenticationContextClasses.MultiFactor))
        {
            context.Succeed(requirement);

            return;
        }

        if (!Guid.TryParse(context.User.FindFirst(AccessTokenClaimTypes.Subject)?.Value, out var userId))
        {
            return;
        }

        var httpContext = context.Resource as HttpContext;

        if (!await policy.IsRequiredAsync(userId, httpContext?.RequestAborted ?? default))
        {
            context.Succeed(requirement);

            return;
        }

        if (httpContext is not null)
        {
            httpContext.Items[InsufficientAuthenticationItem] = true;
        }
    }
}
