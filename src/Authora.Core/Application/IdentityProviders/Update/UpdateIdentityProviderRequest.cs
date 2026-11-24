// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.IdentityProviders.Update;

/// <summary>
///     Represents the request to update an external identity provider. The Slug and the Preset cannot be changed; the
///     Client Secret has an endpoint of its own.
/// </summary>
/// <param name="Name" example="Google">New Name shown on the sign-in button, or null to leave it unchanged.</param>
/// <param name="Issuer" example="https://login.example.com">New Issuer, or null to leave it unchanged. Cannot be changed for the Google preset.</param>
/// <param name="ClientId" example="1234567890-abc.apps.googleusercontent.com">New Client Id at the provider, or null to leave it unchanged.</param>
/// <param name="Scopes">New Scopes to request (<c>openid</c> is always added), or null to leave them unchanged.</param>
/// <param name="SortOrder" example="10">New position on the sign-in page, or null to leave it unchanged.</param>
public sealed record UpdateIdentityProviderRequest(
    [StringLength(IdentityProvider.NameMaxLength, MinimumLength = 1)] string? Name,
    [StringLength(IdentityProvider.IssuerMaxLength, MinimumLength = 1)] string? Issuer,
    [StringLength(IdentityProvider.ClientIdMaxLength, MinimumLength = 1)] string? ClientId,
    IReadOnlyList<string>? Scopes,
    int? SortOrder);
