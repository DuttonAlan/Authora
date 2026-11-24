// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.IdentityProviders.Create;

/// <summary>
///     Represents the response for creating an external identity provider.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the created provider.</param>
/// <param name="RedirectUri" example="https://auth.example.com/account/external/google/callback">The Redirect URI to register at the provider.</param>
public sealed record CreateIdentityProviderResponse(Guid Id, string RedirectUri);
