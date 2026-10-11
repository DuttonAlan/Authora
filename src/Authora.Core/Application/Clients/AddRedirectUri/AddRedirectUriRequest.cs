// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Authora.Core.Application.Clients.AddRedirectUri;

/// <summary>
///     Represents the request to register a Redirect URI for a Client.
/// </summary>
/// <param name="Uri" example="https://shop.example.com/callback">The Redirect URI. Must be absolute, without fragment, and use HTTPS (HTTP only for loopback).</param>
public sealed record AddRedirectUriRequest(
    [Required, StringLength(2048, MinimumLength = 1)] string Uri);
