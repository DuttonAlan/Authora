// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Clients.Update;

/// <summary>
///     Represents the request to update a Client. The Type of a Client cannot be changed.
/// </summary>
/// <param name="Name" example="Shop Web App">New Display Name of the Client, or null to leave it unchanged.</param>
/// <param name="GrantTypes" example="[&quot;authorization_code&quot;, &quot;refresh_token&quot;]">New allowed Grant Types (authorization_code, refresh_token), or null to leave them unchanged. refresh_token requires authorization_code.</param>
/// <param name="AccessTokenTTL" example="3600">New lifetime of Access Tokens in seconds (at most 86400), or null to leave it unchanged.</param>
/// <param name="RefreshTokenTTL" example="86400">New lifetime of Refresh Tokens in seconds (at most 7776000), or null to leave it unchanged. Must be greater than the Access Token TTL.</param>
public sealed record UpdateClientRequest(
    [StringLength(Client.NameMaxLength, MinimumLength = 1)] string? Name,
    IReadOnlyList<string>? GrantTypes,
    [Range(1, Client.MaxAccessTokenTTL)] int? AccessTokenTTL,
    [Range(1, Client.MaxRefreshTokenTTL)] int? RefreshTokenTTL);
