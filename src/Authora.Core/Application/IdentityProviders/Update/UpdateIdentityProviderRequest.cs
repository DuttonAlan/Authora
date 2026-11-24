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
/// <param name="AllowSignUp" example="true">Whether people without a User can sign up through the provider, or null to leave it unchanged. For Google it needs at least one allowed domain.</param>
/// <param name="AllowedEmailDomains">The domains whose people may sign up (replacing the current ones; empty allows every domain, not for Google), or null to leave them unchanged.</param>
/// <param name="DefaultGroupIds">The Ids of the Groups a User who signs up joins (replacing the current ones), or null to leave them unchanged.</param>
public sealed record UpdateIdentityProviderRequest(
    [StringLength(IdentityProvider.NameMaxLength, MinimumLength = 1)] string? Name,
    [StringLength(IdentityProvider.IssuerMaxLength, MinimumLength = 1)] string? Issuer,
    [StringLength(IdentityProvider.ClientIdMaxLength, MinimumLength = 1)] string? ClientId,
    IReadOnlyList<string>? Scopes,
    int? SortOrder,
    bool? AllowSignUp = null,
    IReadOnlyList<string>? AllowedEmailDomains = null,
    IReadOnlyList<Guid>? DefaultGroupIds = null);
