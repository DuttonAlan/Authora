// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Settings of multi-factor authentication, bound from the section <see cref="SectionName"/>.
/// </summary>
public sealed class MfaOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Mfa";

    /// <summary>
    ///     Maximum length of <see cref="IssuerName"/>.
    /// </summary>
    public const int IssuerNameMaxLength = 64;

    /// <summary>
    ///     The name authenticator apps show for the account, next to the Email of the User, e.g. the name of your
    ///     company. Defaults to "Authora". Changing it does not change the entries Users already added.
    /// </summary>
    public string IssuerName { get; set; } = "Authora";

    /// <summary>
    ///     Whether every User who holds a Permission of the admin API must sign in with multi-factor authentication.
    ///     Defaults to <see langword="false"/>, so upgrades keep working; recommended in production.
    /// </summary>
    /// <remarks>
    ///     Such a User without a second factor sets up an authenticator app right after the password, and an
    ///     authorization request with the <c>authora.admin</c> Scope steps up a Session without one. The admin API
    ///     rejects their Access Tokens unless the sign-in they were issued for had two factors, also tokens of other
    ///     Clients.
    /// </remarks>
    public bool RequireForAdministrators { get; set; }
}
