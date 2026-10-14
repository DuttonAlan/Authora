// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.AdminUi.GetSettings;

/// <summary>
///     What the admin interface needs to sign in. All of it is public.
/// </summary>
/// <param name="Issuer" example="https://auth.example.com">The Issuer, to check the <c>iss</c> of the authorization response.</param>
/// <param name="ClientId" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">The Id of the Public Client of the admin interface.</param>
/// <param name="RedirectUri" example="https://auth.example.com/admin/">The Redirect URI of the admin interface.</param>
/// <param name="Scope" example="authora.admin">The Scope to request.</param>
public sealed record AdminUiSettingsResponse(string Issuer, Guid ClientId, string RedirectUri, string Scope);
