// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.ExternalSignIn.GetProviders;

namespace Authora.Core.Application.ExternalSignIn.GetIdentities;

/// <summary>
///     The Service for the account page of a signed-in User: their linked accounts at external identity providers,
///     and the enabled providers they can still link.
/// </summary>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to load the links.</param>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load the providers.</param>
public sealed class GetExternalIdentitiesService(
    IExternalIdentityRepository externalIdentityRepository,
    IIdentityProviderRepository providerRepository)
{
    /// <summary>
    ///     Gets the links and the providers that can be linked.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The links and the providers without a link.</returns>
    public async Task<ExternalIdentitiesResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var identities = await externalIdentityRepository.GetIdentitiesOfUser(userId, cancellationToken);
        var providers = (await providerRepository.GetProviders(enabledOnly: false, cancellationToken)).ToDictionary(provider => provider.Id);

        var linked = identities
            .Select(identity =>
            {
                var provider = providers.GetValueOrDefault(identity.ProviderId);

                return new ExternalIdentityResponse(
                    identity.Id,
                    provider?.Name ?? "Unknown provider",
                    provider?.IsEnabled == true,
                    identity.Email,
                    identity.CreatedAt,
                    identity.LastUsedAt);
            })
            .ToList();

        var available = providers.Values
            .Where(provider => provider.IsEnabled && identities.All(identity => identity.ProviderId != provider.Id))
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.Name, StringComparer.Ordinal)
            .Select(provider => new SignInProviderResponse(provider.Id, provider.Name, provider.Slug))
            .ToList();

        return new ExternalIdentitiesResponse(linked, available);
    }
}

/// <summary>
///     The linked accounts of a User and the providers they can still link.
/// </summary>
/// <param name="Linked">The linked accounts, in the order they were linked.</param>
/// <param name="Available">The enabled providers without a link.</param>
public sealed record ExternalIdentitiesResponse(
    IReadOnlyList<ExternalIdentityResponse> Linked,
    IReadOnlyList<SignInProviderResponse> Available);

/// <summary>
///     A linked account at an external identity provider.
/// </summary>
/// <param name="Id">Id of the link.</param>
/// <param name="ProviderName">Name of the provider.</param>
/// <param name="IsProviderEnabled">Whether the provider is enabled, so the User can sign in through it.</param>
/// <param name="Email">The Email the provider reported when it was linked, or null.</param>
/// <param name="CreatedAt">Point in time (UTC) when it was linked.</param>
/// <param name="LastUsedAt">Point in time (UTC) when the User last signed in through it, or null.</param>
public sealed record ExternalIdentityResponse(
    Guid Id,
    string ProviderName,
    bool IsProviderEnabled,
    string? Email,
    DateTime CreatedAt,
    DateTime? LastUsedAt);
