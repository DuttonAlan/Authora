// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.ExternalSignIn;

/// <summary>
///     An account at an external identity provider that is not linked yet, but whose verified Email belongs to a User.
///     It is only linked once the browser signs in to that User with the User's existing methods (password and second
///     factor, if set up); a matching Email alone never links. Kept by the browser in an encrypted, short-lived cookie.
/// </summary>
/// <param name="ProviderId">Id of the provider.</param>
/// <param name="Slug">Slug of the provider.</param>
/// <param name="Subject">The Subject of the account at the provider.</param>
/// <param name="Email">The Email the provider reported.</param>
/// <param name="UserId">Id of the User with this Email, the only User the account can be linked to.</param>
/// <param name="CreatedAt">Point in time (UTC) when the account arrived; the sign-in must be later.</param>
/// <param name="ReturnUrl">The local URL to continue with after linking, e.g. the pending authorization request.</param>
public sealed record PendingExternalLink(
    Guid ProviderId,
    string Slug,
    string Subject,
    string Email,
    Guid UserId,
    DateTime CreatedAt,
    string ReturnUrl)
{
    /// <summary>
    ///     Returns a description without the Subject and the Email.
    /// </summary>
    /// <returns>A description of the pending link.</returns>
    public override string ToString() => $"PendingExternalLink(Slug = {Slug}, UserId = {UserId})";
}
