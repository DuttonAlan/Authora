// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.IdentityProviders.Get;

/// <summary>
///     The Service to get an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the provider.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer of Authora, for the Redirect URI.</param>
public sealed class GetIdentityProviderService(
    IIdentityProviderRepository providerRepository,
    IOptions<OAuthOptions> oauthOptions)
{
    /// <summary>
    ///     Gets a provider based on the Id.
    /// </summary>
    /// <param name="id">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The provider, or null if it does not exist.</returns>
    public async Task<IdentityProviderResponse?> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await providerRepository.GetProviderById(id, cancellationToken);

        if (provider is null)
        {
            return null;
        }

        var linked = await providerRepository.CountExternalIdentities(provider.Id, cancellationToken);

        return IdentityProviderResponse.From(provider, oauthOptions.Value.Issuer, linked);
    }
}
