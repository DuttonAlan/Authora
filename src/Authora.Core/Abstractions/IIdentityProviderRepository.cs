// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for external identity provider operations.
/// </summary>
/// <remarks>
///     Providers are changed by loading them tracked via <see cref="GetProviderById"/>, changing them through their
///     methods and calling <see cref="SaveChangesAsync"/>.
/// </remarks>
public interface IIdentityProviderRepository
{
    /// <summary>
    ///     Creates a new identity provider.
    /// </summary>
    /// <param name="provider">The provider to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the provider is saved.</returns>
    public Task CreateProvider(IdentityProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets an identity provider by Id.
    /// </summary>
    /// <param name="id">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked provider, or null if it does not exist.</returns>
    public Task<IdentityProvider?> GetProviderById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets an identity provider by its Slug.
    /// </summary>
    /// <param name="slug">The Slug, compared case-sensitively.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The provider without tracking, or null if it does not exist.</returns>
    public Task<IdentityProvider?> GetProviderBySlug(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all identity providers, ordered by <see cref="IdentityProvider.SortOrder"/> and Name.
    /// </summary>
    /// <param name="enabledOnly">True to get only the enabled providers.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The providers without tracking.</returns>
    public Task<IReadOnlyList<IdentityProvider>> GetProviders(bool enabledOnly, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a provider with the given Slug exists.
    /// </summary>
    /// <param name="slug">The Slug.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Slug is already used.</returns>
    public Task<bool> SlugExists(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the external identities linked through a provider.
    /// </summary>
    /// <param name="providerId">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of linked identities.</returns>
    public Task<int> CountExternalIdentities(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes an identity provider.
    /// </summary>
    /// <param name="provider">The tracked provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the provider is deleted.</returns>
    /// <exception cref="Application.Exceptions.ConflictException">
    ///     Thrown when an identity was linked through the provider in the meantime.
    /// </exception>
    public Task DeleteProvider(IdentityProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked providers.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
