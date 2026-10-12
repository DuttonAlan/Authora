// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Clients.Register;

/// <summary>
///     Represents the request to register a Client.
/// </summary>
/// <param name="Name" example="Shop Web App">Display Name of the Client, e.g. shown on the consent screen.</param>
/// <param name="Type" example="Confidential">Type of the Client. Public Clients have no Secret. Cannot be changed later.</param>
/// <param name="GrantTypes" example="[&quot;authorization_code&quot;, &quot;refresh_token&quot;]">Allowed Grant Types: authorization_code and refresh_token. refresh_token requires authorization_code. Defaults to both.</param>
/// <param name="AccessTokenTTL" example="3600">Lifetime of Access Tokens in seconds, at most 86400 (24 hours). Defaults to 3600 (1 hour).</param>
/// <param name="RefreshTokenTTL" example="86400">Lifetime of Refresh Tokens in seconds, at most 7776000 (90 days). Must be greater than the Access Token TTL. Defaults to 86400 (24 hours).</param>
/// <param name="RedirectUris" example="[&quot;https://shop.example.com/callback&quot;]">Redirect URIs to register. Must be absolute, without fragment, and use HTTPS (HTTP only for loopback).</param>
/// <param name="ScopeIds">Ids of the Scopes the Client is allowed to request.</param>
public sealed record RegisterClientRequest(
    [Required, StringLength(Client.NameMaxLength, MinimumLength = 1)] string Name,
    [Required] ClientType? Type,
    IReadOnlyList<string>? GrantTypes,
    [Range(1, Client.MaxAccessTokenTTL)] int? AccessTokenTTL,
    [Range(1, Client.MaxRefreshTokenTTL)] int? RefreshTokenTTL,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<Guid>? ScopeIds);
