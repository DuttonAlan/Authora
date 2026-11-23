// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Clients.Get;

/// <summary>
///     Represents the response for getting a Client.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-5c963f66afa6">Id of the Client, used as its OAuth client_id.</param>
/// <param name="Name" example="Shop Web App">Display Name of the Client.</param>
/// <param name="Type" example="Confidential">Type of the Client.</param>
/// <param name="Status" example="Enabled">Status of the Client. Disabled Clients cannot obtain new tokens.</param>
/// <param name="IsFirstParty" example="false">Whether the Client is first-party, so Users are not asked for consent.</param>
/// <param name="IsPkceOptional" example="false">Whether the Client may send authorization requests without PKCE. Only possible for Confidential Clients.</param>
/// <param name="RequiresMfa" example="false">Whether Users must sign in with multi-factor authentication to use the Client.</param>
/// <param name="GrantTypes" example="[&quot;authorization_code&quot;, &quot;refresh_token&quot;]">Allowed Grant Types.</param>
/// <param name="AccessTokenTTL" example="3600">Lifetime of Access Tokens in seconds.</param>
/// <param name="RefreshTokenTTL" example="86400">Lifetime of Refresh Tokens in seconds.</param>
/// <param name="RedirectUris" example="[&quot;https://shop.example.com/callback&quot;]">Registered Redirect URIs.</param>
/// <param name="PostLogoutRedirectUris" example="[&quot;https://shop.example.com/signed-out&quot;]">Registered Post-logout Redirect URIs, for RP-initiated logout.</param>
/// <param name="BackchannelLogoutUri" example="https://shop.example.com/backchannel-logout">Back-Channel Logout URI, to which Authora sends a Logout Token when a Session ends in which the Client received tokens, or null.</param>
/// <param name="BackchannelLogoutSessionRequired" example="false">Whether the Client requires the sid claim in Logout Tokens. Authora always sends it.</param>
/// <param name="Scopes">Scopes the Client is allowed to request.</param>
/// <param name="ServedScopes">Scopes the Client serves as a resource server. It can introspect Access Tokens of other Clients only if they contain one of these Scopes.</param>
/// <param name="Secrets">Secrets of the Client, including expired and revoked ones. Only metadata, never the Secret itself.</param>
/// <param name="CreatedAt">Point in time (UTC) when the Client was created.</param>
/// <param name="ModifiedAt">Point in time (UTC) when the Client was last modified, or null if it was never modified.</param>
public sealed record GetClientResponse(
    Guid Id,
    string Name,
    ClientType Type,
    ClientStatus Status,
    bool IsFirstParty,
    bool IsPkceOptional,
    bool RequiresMfa,
    IReadOnlyList<string> GrantTypes,
    int AccessTokenTTL,
    int RefreshTokenTTL,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? BackchannelLogoutUri,
    bool BackchannelLogoutSessionRequired,
    IReadOnlyList<ClientScopeResponse> Scopes,
    IReadOnlyList<ClientScopeResponse> ServedScopes,
    IReadOnlyList<ClientSecretResponse> Secrets,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
