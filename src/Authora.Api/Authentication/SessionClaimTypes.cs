// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.Authentication;

/// <summary>
///     Claim types of the principal created by <see cref="SessionAuthenticationHandler"/>.
/// </summary>
internal static class SessionClaimTypes
{
    /// <summary>
    ///     Id of the User.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    ///     Id of the Session.
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>
    ///     Email of the User.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    ///     Full name of the User.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    ///     When the User signed in, in seconds since the Unix epoch.
    /// </summary>
    public const string AuthenticationTime = "auth_time";
}
