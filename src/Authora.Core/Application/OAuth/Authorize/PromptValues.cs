// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.OAuth.Authorize;

/// <summary>
///     The supported values of the OpenID Connect <c>prompt</c> parameter (OpenID Connect Core 1.0, Section 3.1.2.1).
///     Other values are ignored.
/// </summary>
public static class PromptValues
{
    /// <summary>
    ///     Show no page: answer with <see cref="OAuthErrors.LoginRequired"/> or <see cref="OAuthErrors.ConsentRequired"/>
    ///     instead. Must not be combined with other values.
    /// </summary>
    public const string None = "none";

    /// <summary>
    ///     The User must sign in again, even if signed in.
    /// </summary>
    public const string Login = "login";

    /// <summary>
    ///     Ask for consent. Authora always does, so it changes nothing.
    /// </summary>
    public const string Consent = "consent";

    /// <summary>
    ///     Let the User choose an account. Authora has no account chooser, so the User signs in again and can use
    ///     another account.
    /// </summary>
    public const string SelectAccount = "select_account";

    /// <summary>
    ///     All supported values, for the discovery document.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [None, Login, Consent, SelectAccount];
}
