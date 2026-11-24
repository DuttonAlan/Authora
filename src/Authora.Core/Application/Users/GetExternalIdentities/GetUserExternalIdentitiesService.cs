// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Users.GetExternalIdentities;

/// <summary>
///     The Service for administrators to list the accounts at external identity providers linked to a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to check that the User exists.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to load the links.</param>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to name the providers.</param>
public sealed class GetUserExternalIdentitiesService(
    IUserRepository userRepository,
    IExternalIdentityRepository externalIdentityRepository,
    IIdentityProviderRepository providerRepository)
{
    /// <summary>
    ///     Gets the linked accounts of a User, in the order they were linked. The Subjects at the providers are not
    ///     returned.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The linked accounts, empty if there are none.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<UserExternalIdentityResponse>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _ = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var identities = await externalIdentityRepository.GetIdentitiesOfUser(userId, cancellationToken);

        if (identities.Count == 0)
        {
            return [];
        }

        var providers = (await providerRepository.GetProviders(enabledOnly: false, cancellationToken)).ToDictionary(provider => provider.Id);

        return [.. identities.Select(identity =>
        {
            var provider = providers.GetValueOrDefault(identity.ProviderId);

            return new UserExternalIdentityResponse(
                identity.Id,
                identity.ProviderId,
                provider?.Name ?? "Unknown provider",
                provider?.Slug,
                provider?.IsEnabled == true,
                identity.Email,
                identity.CreatedAt,
                identity.LastUsedAt);
        })];
    }
}

/// <summary>
///     An account at an external identity provider linked to a User.
/// </summary>
/// <param name="Id" example="0199d1a2-7c3e-7b4a-9f10-2c963f66afa6">Id of the link.</param>
/// <param name="ProviderId" example="0199d1a2-7c3e-7b4a-9f10-4c963f66afa6">Id of the identity provider.</param>
/// <param name="ProviderName" example="Google">Name of the identity provider.</param>
/// <param name="ProviderSlug" example="google">Slug of the identity provider.</param>
/// <param name="IsProviderEnabled" example="true">Whether the provider is enabled, so the User can sign in through it.</param>
/// <param name="Email" example="jane.doe@example.com">The Email the provider reported when the account was linked, or null. Only for display.</param>
/// <param name="CreatedAt">Point in time (UTC) when the account was linked.</param>
/// <param name="LastUsedAt">Point in time (UTC) when the User last signed in through it, or null.</param>
public sealed record UserExternalIdentityResponse(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string? ProviderSlug,
    bool IsProviderEnabled,
    string? Email,
    DateTime CreatedAt,
    DateTime? LastUsedAt);
