// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.IdentityProviders.GetAll;

/// <summary>
///     The Service to get all external identity providers.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the providers.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer of Authora, for the Redirect URIs.</param>
public sealed class GetAllIdentityProvidersService(
    IIdentityProviderRepository providerRepository,
    IOptions<OAuthOptions> oauthOptions)
{
    /// <summary>
    ///     Gets all providers in the order of the sign-in page.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All providers, or an empty list.</returns>
    public async Task<IReadOnlyList<IdentityProviderResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var providers = await providerRepository.GetProviders(enabledOnly: false, cancellationToken);
        var responses = new List<IdentityProviderResponse>(providers.Count);

        // Few providers exist, so one count each is cheap enough.
        foreach (var provider in providers)
        {
            var linked = await providerRepository.CountExternalIdentities(provider.Id, cancellationToken);

            responses.Add(IdentityProviderResponse.From(provider, oauthOptions.Value.Issuer, linked));
        }

        return responses;
    }
}
