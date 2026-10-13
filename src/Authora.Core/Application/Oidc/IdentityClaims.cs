// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Oidc;

/// <summary>
///     The identity claims of a User that Authora releases through OpenID Connect (US-026), in ID Tokens and at the
///     UserInfo endpoint.
/// </summary>
/// <remarks>
///     <para>
///         Both use <see cref="Get(User, IReadOnlyCollection{string})"/>, so their claims are always consistent.
///         A claim is only released if the token has the Scope that contains it. Claims Authora has no value for
///         (e.g. <c>picture</c> or <c>birthdate</c>) are never returned, not even as <c>null</c>.
///     </para>
///     <para>
///         Roles and Permissions are never released, see TD-006.
///     </para>
/// </remarks>
public static class IdentityClaims
{
    /// <summary>
    ///     The Id of the User. Released with <see cref="OidcScopes.OpenId"/>.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>
    ///     First and Last Name of the User. Released with <see cref="OidcScopes.Profile"/>.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    ///     First Name of the User. Released with <see cref="OidcScopes.Profile"/>.
    /// </summary>
    public const string GivenName = "given_name";

    /// <summary>
    ///     Last Name of the User. Released with <see cref="OidcScopes.Profile"/>.
    /// </summary>
    public const string FamilyName = "family_name";

    /// <summary>
    ///     Email of the User. Released with <see cref="OidcScopes.Email"/>.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    ///     Whether the User proved to own the Email. Always <see langword="false"/>, because Authora does not verify
    ///     Emails yet. Released with <see cref="OidcScopes.Email"/>.
    /// </summary>
    public const string EmailVerified = "email_verified";

    /// <summary>
    ///     All identity claims Authora can release, for the discovery document.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Subject, Name, GivenName, FamilyName, Email, EmailVerified];

    /// <summary>
    ///     Returns the claims of a User that the Scopes release. <see cref="Subject"/> is always included.
    /// </summary>
    /// <param name="user">The User.</param>
    /// <param name="scopes">The Scope Names of the token.</param>
    /// <returns>The claims by name, with string or boolean values.</returns>
    public static Dictionary<string, object> Get(User user, IReadOnlyCollection<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(scopes);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Subject] = FormatSubject(user.Id)
        };

        if (scopes.Contains(OidcScopes.Profile))
        {
            claims[Name] = $"{user.FirstName} {user.LastName}";
            claims[GivenName] = user.FirstName;
            claims[FamilyName] = user.LastName;
        }

        if (scopes.Contains(OidcScopes.Email))
        {
            claims[Email] = user.Email;
            claims[EmailVerified] = false;
        }

        return claims;
    }

    /// <summary>
    ///     Formats the Id of a User as <c>sub</c>. The same for every Client, so it identifies the User within the
    ///     Issuer (public subject identifier, OpenID Connect Core 1.0, Section 8).
    /// </summary>
    /// <param name="userId">The Id of the User.</param>
    /// <returns>The <c>sub</c> value.</returns>
    public static string FormatSubject(Guid userId) => userId.ToString("D");
}
