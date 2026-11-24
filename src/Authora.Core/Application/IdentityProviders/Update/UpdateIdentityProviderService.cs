// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders.Update;

/// <summary>
///     The Service to update an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load and save the provider.</param>
/// <param name="groupRepository">An Instance of a IGroupRepository to load the default Groups.</param>
/// <param name="issuerValidator">Checks the discovery document of a new Issuer.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the update.</param>
public sealed class UpdateIdentityProviderService(
    IIdentityProviderRepository providerRepository,
    IGroupRepository groupRepository,
    IssuerValidator issuerValidator,
    IAuditLog auditLog,
    ILogger<UpdateIdentityProviderService> logger)
{
    /// <summary>
    ///     Updates the fields that are set in the request. A new Issuer is checked by fetching its discovery document
    ///     once.
    /// </summary>
    /// <param name="id">Id of the provider.</param>
    /// <param name="request">The request to update the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the provider is updated.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider does not exist.</exception>
    /// <exception cref="Domain.Exceptions.DomainValidationException">Thrown when a value is invalid or the new Issuer has no usable discovery document.</exception>
    public async Task ExecuteAsync(Guid id, UpdateIdentityProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = await providerRepository.GetProviderById(id, cancellationToken)
            ?? throw new NotFoundException($"Identity provider '{id}' was not found.");

        if (request.Name is not null)
        {
            provider.SetName(request.Name);
        }

        if (request.ClientId is not null)
        {
            provider.SetClientId(request.ClientId);
        }

        if (request.Scopes is not null)
        {
            provider.SetScopes(string.Join(' ', request.Scopes));
        }

        if (request.SortOrder is { } sortOrder)
        {
            provider.SetSortOrder(sortOrder);
        }

        await SignUpSettings.ApplyAsync(
            provider,
            request.AllowSignUp,
            request.AllowedEmailDomains,
            request.DefaultGroupIds,
            groupRepository,
            cancellationToken);

        if (request.Issuer is not null)
        {
            var previous = provider.Issuer;

            provider.SetIssuer(request.Issuer);

            if (!string.Equals(previous, provider.Issuer, StringComparison.Ordinal))
            {
                await issuerValidator.EnsureValidAsync(provider.Issuer, cancellationToken);
            }
        }

        auditLog.Record(AuditEntry.ForIdentityProvider(
            AuditEventTypes.IdentityProviders.Updated,
            provider.Id,
            $"Slug '{provider.Slug}', Issuer '{provider.Issuer}', {SignUpSettings.Describe(provider)}"));

        await providerRepository.SaveChangesAsync(cancellationToken);

        logger.IdentityProviderUpdated(provider.Id, provider.Slug);
    }
}
