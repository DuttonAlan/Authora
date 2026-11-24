// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.ExternalSignIn.GetProviders;

/// <summary>
///     The Service for the sign-in page: the enabled external identity providers, one button each.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the providers.</param>
public sealed class GetSignInProvidersService(IIdentityProviderRepository providerRepository)
{
    /// <summary>
    ///     Gets the enabled providers in the order of the sign-in page.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The providers, or an empty list.</returns>
    public async Task<IReadOnlyList<SignInProviderResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var providers = await providerRepository.GetProviders(enabledOnly: true, cancellationToken);

        return [.. providers.Select(provider => new SignInProviderResponse(provider.Id, provider.Name, provider.Slug))];
    }
}

/// <summary>
///     An enabled external identity provider, as the account pages show it.
/// </summary>
/// <param name="Id">Id of the provider.</param>
/// <param name="Name">Name shown on the button.</param>
/// <param name="Slug">Slug in the URLs of the flow.</param>
public sealed record SignInProviderResponse(Guid Id, string Name, string Slug);
