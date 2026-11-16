// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Authora.Core.Application.Clients.AddPostLogoutRedirectUri;

/// <summary>
///     Represents the request to register a Post-logout Redirect URI for a Client.
/// </summary>
/// <param name="Uri" example="https://shop.example.com/signed-out">The Post-logout Redirect URI. Must be absolute, without fragment, and use HTTPS (HTTP only for loopback).</param>
public sealed record AddPostLogoutRedirectUriRequest(
    [Required, StringLength(2048, MinimumLength = 1)] string Uri);
