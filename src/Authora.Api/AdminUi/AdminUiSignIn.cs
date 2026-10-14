// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.AdminUi;

/// <summary>
///     A running sign-in of the admin interface, kept in the encrypted cookie <see cref="AdminUiCookies.SignInName"/>.
/// </summary>
/// <param name="State">The <c>state</c> of the authorization request.</param>
/// <param name="CodeVerifier">The PKCE <c>code_verifier</c>. Never leaves the server.</param>
/// <param name="ReturnTo">The view to show after signing in, e.g. <c>#/users</c>, or an empty string.</param>
public sealed record AdminUiSignIn(string State, string CodeVerifier, string ReturnTo)
{
    /// <summary>
    ///     Returns a description without the verifier, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the sign-in.</returns>
    public override string ToString() => $"AdminUiSignIn({ReturnTo})";
}
