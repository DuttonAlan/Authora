// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Oidc;

/// <summary>
///     The standard Scopes of OpenID Connect (OpenID Connect Core 1.0, Sections 3.1.2.1 and 5.4). They are seeded as
///     System Scopes, and a Client must be allowed them like any other Scope.
/// </summary>
/// <remarks>
///     Which identity claims a Client receives is controlled by these Scopes: an administrator allows them per Client,
///     and the User approves them on the consent screen. See <see cref="IdentityClaims"/> for the claims of each Scope.
/// </remarks>
public static class OidcScopes
{
    /// <summary>
    ///     Makes the request an OpenID Connect request: the token response contains an ID Token, and the Access Token
    ///     can be used at the UserInfo endpoint.
    /// </summary>
    public const string OpenId = "openid";

    /// <summary>
    ///     Releases the name claims of the User.
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    ///     Releases the Email of the User.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    ///     The Scopes with their Display Name and Description for the consent screen, in the order they are seeded.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string DisplayName, string Description)> All =
    [
        (OpenId, "Sign you in", "Learn your account Id, so the application can sign you in."),
        (Profile, "Your name", "See your first and last name."),
        (Email, "Your email address", "See your email address.")
    ];
}
