// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders.Delete;

/// <summary>
///     The Service to delete an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to delete the provider.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteIdentityProviderService(
    IIdentityProviderRepository providerRepository,
    IAuditLog auditLog,
    ILogger<DeleteIdentityProviderService> logger)
{
    /// <summary>
    ///     Deletes a provider based on the Id. Does nothing if it does not exist.
    /// </summary>
    /// <param name="id">Id of the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the provider is deleted.</returns>
    /// <exception cref="ConflictException">Thrown when identities are linked through the provider.</exception>
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await providerRepository.GetProviderById(id, cancellationToken);

        if (provider is null)
        {
            return;
        }

        var linked = await providerRepository.CountExternalIdentities(provider.Id, cancellationToken);

        if (linked > 0)
        {
            throw new ConflictException(
                $"{linked} account(s) are linked through the identity provider. Disable it instead, or remove the links first.");
        }

        auditLog.Record(AuditEntry.ForIdentityProvider(
            AuditEventTypes.IdentityProviders.Deleted,
            provider.Id,
            $"Slug '{provider.Slug}', Issuer '{provider.Issuer}'"));

        // Throws a ConflictException if an identity was linked in the meantime; the database refuses the deletion.
        await providerRepository.DeleteProvider(provider, cancellationToken);

        logger.IdentityProviderDeleted(provider.Id, provider.Slug);
    }
}
