// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.Oidc;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.BackchannelLogout;

/// <summary>
///     Creates Logout Tokens (OpenID Connect Back-Channel Logout 1.0, Section 2.4), which tell a Client that a Session
///     of a User ended.
/// </summary>
/// <remarks>
///     Signed with the key of the ID Tokens, but with the header <c>typ: logout+jwt</c>, so a Logout Token is never
///     accepted as an ID Token and vice versa. It always contains <c>sub</c> and <c>sid</c>, never a <c>nonce</c>.
/// </remarks>
/// <param name="signingKeyProvider">The provider of the signing key.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer.</param>
public sealed class LogoutTokenIssuer(ISigningKeyProvider signingKeyProvider, IOptions<OAuthOptions> oauthOptions)
{
    /// <summary>
    ///     The event of a back-channel logout, the only member of the <c>events</c> claim.
    /// </summary>
    public const string BackchannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    /// <summary>
    ///     Lifetime of a Logout Token. It is created right before it is sent, so it only needs to cover the delivery.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Creates the Logout Token of a notification.
    /// </summary>
    /// <param name="notification">The notification with the Client, the User and the Session ID.</param>
    /// <param name="now">The current point in time (UTC).</param>
    /// <returns>The signed Logout Token.</returns>
    public string Issue(BackchannelLogoutNotification notification, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var claims = new Dictionary<string, object>
        {
            ["iss"] = oauthOptions.Value.Issuer,
            ["aud"] = notification.ClientId.ToString("D"),
            ["iat"] = ToUnixTime(now),
            ["exp"] = ToUnixTime(now + Lifetime),

            // New for every attempt: a Client that rejects a jti it has seen would otherwise reject every retry after a
            // delivery that it received, but whose response was lost.
            ["jti"] = Guid.CreateVersion7().ToString("D"),
            ["sub"] = notification.Subject,
            ["sid"] = notification.Sid,
            ["events"] = new Dictionary<string, object>
            {
                [BackchannelLogoutEvent] = new Dictionary<string, object>()
            }
        };

        return JsonWebToken.Create(claims, signingKeyProvider.SigningKey, JsonWebToken.LogoutTokenType);
    }

    private static long ToUnixTime(DateTime value) => new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds();
}
