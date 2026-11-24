// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.IdentityProviders.Create;

/// <summary>
///     Represents the request to create an external identity provider.
/// </summary>
/// <param name="Name" example="Google">Name shown on the sign-in button.</param>
/// <param name="Slug" example="google">Unique identifier in URLs: lowercase letters, digits and hyphens. Part of the Redirect URI, so it cannot be changed later.</param>
/// <param name="Preset" example="Google">The kind of provider. <c>Google</c> fixes the Issuer to <c>https://accounts.google.com</c>.</param>
/// <param name="Issuer" example="https://login.example.com">The Issuer of the provider. Required for <c>Generic</c>, ignored for <c>Google</c>.</param>
/// <param name="ClientId" example="1234567890-abc.apps.googleusercontent.com">The Client Id of Authora at the provider.</param>
/// <param name="ClientSecret" example="GOCSPX-…">The Client Secret of Authora at the provider, or null if Authora is a Public Client there. Stored encrypted and never returned.</param>
/// <param name="Scopes">The Scopes to request; <c>openid</c> is always added. Defaults to <c>openid email profile</c>.</param>
/// <param name="SortOrder" example="0">Position of the button on the sign-in page; lower values first. Defaults to 0.</param>
/// <param name="IsEnabled" example="true">Whether Users can sign in through it right away. Defaults to true.</param>
public sealed record CreateIdentityProviderRequest(
    [Required, StringLength(IdentityProvider.NameMaxLength, MinimumLength = 1)] string Name,
    [Required, StringLength(IdentityProvider.SlugMaxLength, MinimumLength = 1), RegularExpression(IdentityProvider.SlugPattern)] string Slug,
    IdentityProviderPreset Preset,
    [StringLength(IdentityProvider.IssuerMaxLength)] string? Issuer,
    [Required, StringLength(IdentityProvider.ClientIdMaxLength, MinimumLength = 1)] string ClientId,
    [StringLength(IdentityProvider.ClientSecretMaxLength, MinimumLength = 1)] string? ClientSecret,
    IReadOnlyList<string>? Scopes,
    int? SortOrder,
    bool? IsEnabled)
{
    /// <summary>
    ///     Returns a description without the Client Secret, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => $"CreateIdentityProviderRequest({Slug}, {Preset}, {Issuer}, ***)";
}
