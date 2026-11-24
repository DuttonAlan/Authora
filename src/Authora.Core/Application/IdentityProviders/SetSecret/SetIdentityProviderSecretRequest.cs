// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.IdentityProviders.SetSecret;

/// <summary>
///     Represents the request to set the Client Secret of an external identity provider.
/// </summary>
/// <param name="ClientSecret" example="GOCSPX-…">The Client Secret of Authora at the provider. Stored encrypted and never returned.</param>
public sealed record SetIdentityProviderSecretRequest(
    [Required, StringLength(IdentityProvider.ClientSecretMaxLength, MinimumLength = 1)] string ClientSecret)
{
    /// <summary>
    ///     Returns a description without the Client Secret, so it is not written to logs by accident.
    /// </summary>
    /// <returns>A description of the request.</returns>
    public override string ToString() => "SetIdentityProviderSecretRequest(***)";
}
