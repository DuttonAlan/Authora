// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;

namespace Authora.Api.Auditing;

/// <summary>
///     Provides the actor of audit events from the current HTTP request: the User of the Access Token or the Session,
///     the Client of the Access Token, and the IP address.
/// </summary>
/// <remarks>
///     Behind a reverse proxy, the IP address is only the address of the client if the forwarded headers are
///     configured (<c>Authora:ForwardedHeaders</c>), otherwise it is the address of the proxy.
/// </remarks>
/// <param name="httpContextAccessor">Provides the current request.</param>
internal sealed class HttpContextAuditActorAccessor(IHttpContextAccessor httpContextAccessor) : IAuditActorAccessor
{
    /// <inheritdoc/>
    public AuditActor Current
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;

            if (httpContext is null)
            {
                return AuditActor.System;
            }

            // Both schemes use "sub" for the User, see AccessTokenClaimTypes and SessionClaimTypes.
            var user = httpContext.User;
            var userId = user.Identity?.IsAuthenticated == true
                && Guid.TryParse(user.FindFirst(AccessTokenClaimTypes.Subject)?.Value, out var subject)
                    ? subject
                    : (Guid?)null;
            var clientId = Guid.TryParse(user.FindFirst(AccessTokenClaimTypes.ClientId)?.Value, out var client)
                ? client
                : (Guid?)null;

            return new AuditActor(userId, clientId, httpContext.Connection.RemoteIpAddress?.ToString());
        }
    }
}
