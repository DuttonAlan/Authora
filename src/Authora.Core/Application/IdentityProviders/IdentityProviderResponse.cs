// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.ExternalSignIn;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.IdentityProviders;

/// <summary>
///     An external identity provider. The Client Secret is never returned, only whether one is set.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the provider.</param>
/// <param name="Name" example="Google">Name shown on the sign-in button.</param>
/// <param name="Slug" example="google">Unique identifier in URLs; part of the Redirect URI.</param>
/// <param name="Preset" example="Google">The kind of provider.</param>
/// <param name="Issuer" example="https://accounts.google.com">The Issuer of the provider.</param>
/// <param name="ClientId" example="1234567890-abc.apps.googleusercontent.com">The Client Id of Authora at the provider.</param>
/// <param name="HasClientSecret" example="true">Whether a Client Secret is set.</param>
/// <param name="Scopes">The Scopes requested from the provider.</param>
/// <param name="IsEnabled" example="true">Whether Users can sign in through the provider.</param>
/// <param name="SortOrder" example="0">Position of the button on the sign-in page; lower values first.</param>
/// <param name="RedirectUri" example="https://auth.example.com/account/external/google/callback">The Redirect URI to register at the provider.</param>
/// <param name="LinkedIdentities" example="12">How many accounts at the provider are linked to Users.</param>
/// <param name="CreatedAt">Point in time (UTC) when the provider was created.</param>
/// <param name="ModifiedAt">Point in time (UTC) when the provider was last modified, or null.</param>
public sealed record IdentityProviderResponse(
    Guid Id,
    string Name,
    string Slug,
    IdentityProviderPreset Preset,
    string Issuer,
    string ClientId,
    bool HasClientSecret,
    IReadOnlyList<string> Scopes,
    bool IsEnabled,
    int SortOrder,
    string RedirectUri,
    int LinkedIdentities,
    DateTime CreatedAt,
    DateTime? ModifiedAt)
{
    /// <summary>
    ///     Creates the response for a provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="issuer">The Issuer of Authora, for the Redirect URI.</param>
    /// <param name="linkedIdentities">How many identities are linked through the provider.</param>
    /// <returns>The response.</returns>
    public static IdentityProviderResponse From(IdentityProvider provider, string issuer, int linkedIdentities)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new IdentityProviderResponse(
            provider.Id,
            provider.Name,
            provider.Slug,
            provider.Preset,
            provider.Issuer,
            provider.ClientId,
            provider.HasClientSecret,
            provider.ScopeList,
            provider.IsEnabled,
            provider.SortOrder,
            ExternalSignInOptions.GetRedirectUri(issuer, provider.Slug),
            linkedIdentities,
            provider.CreatedAt,
            provider.ModifiedAt);
    }
}
