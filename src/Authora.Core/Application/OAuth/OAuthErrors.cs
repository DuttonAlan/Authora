// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth;

/// <summary>
///     The <c>error</c> codes of OAuth error responses (RFC 6749, Sections 4.1.2.1 and 5.2, RFC 7009, Section 2.2.1).
/// </summary>
public static class OAuthErrors
{
    /// <summary>
    ///     The request is missing a required parameter, includes an invalid value or a parameter more than once.
    /// </summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>
    ///     Client authentication failed, e.g. an unknown Client, a wrong Secret or a disabled Client.
    /// </summary>
    public const string InvalidClient = "invalid_client";

    /// <summary>
    ///     The Authorization Code or Refresh Token is invalid, expired, revoked, already used, or was issued to
    ///     another Client, or the PKCE verification failed.
    /// </summary>
    public const string InvalidGrant = "invalid_grant";

    /// <summary>
    ///     The Client is not allowed to use this Grant Type or response type.
    /// </summary>
    public const string UnauthorizedClient = "unauthorized_client";

    /// <summary>
    ///     The Grant Type is not supported by Authora.
    /// </summary>
    public const string UnsupportedGrantType = "unsupported_grant_type";

    /// <summary>
    ///     The response type is not supported by Authora.
    /// </summary>
    public const string UnsupportedResponseType = "unsupported_response_type";

    /// <summary>
    ///     The requested Scope is invalid, unknown, or not allowed for the Client.
    /// </summary>
    public const string InvalidScope = "invalid_scope";

    /// <summary>
    ///     The User denied the request.
    /// </summary>
    public const string AccessDenied = "access_denied";
}
