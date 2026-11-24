// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders.Create;

/// <summary>
///     The Service to create an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to save the provider.</param>
/// <param name="groupRepository">An Instance of a IGroupRepository to load the default Groups.</param>
/// <param name="secretProtector">Encrypts the Client Secret.</param>
/// <param name="issuerValidator">Checks the discovery document of the Issuer.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the creation.</param>
public sealed class CreateIdentityProviderService(
    IIdentityProviderRepository providerRepository,
    IGroupRepository groupRepository,
    IProviderSecretProtector secretProtector,
    IssuerValidator issuerValidator,
    IAuditLog auditLog,
    ILogger<CreateIdentityProviderService> logger)
{
    /// <summary>
    ///     Creates a provider after fetching the discovery document of its Issuer once.
    /// </summary>
    /// <param name="request">The request to create the provider.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created provider.</returns>
    /// <exception cref="Domain.Exceptions.DomainValidationException">Thrown when a value is invalid or the Issuer has no usable discovery document.</exception>
    /// <exception cref="ConflictException">Thrown when the Slug is already used.</exception>
    public async Task<Guid> ExecuteAsync(CreateIdentityProviderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = IdentityProvider.Create(
            request.Name,
            request.Slug,
            request.Preset,
            request.Issuer,
            request.ClientId,
            request.Scopes is null ? null : string.Join(' ', request.Scopes),
            request.SortOrder ?? 0);

        if (await providerRepository.SlugExists(provider.Slug, cancellationToken))
        {
            throw new ConflictException("The Slug is already used by another identity provider.");
        }

        if (!string.IsNullOrEmpty(request.ClientSecret))
        {
            provider.SetProtectedClientSecret(secretProtector.Protect(request.ClientSecret));
        }

        if (request.IsEnabled != false)
        {
            provider.Enable();
        }

        await SignUpSettings.ApplyAsync(
            provider,
            request.AllowSignUp,
            request.AllowedEmailDomains,
            request.DefaultGroupIds,
            groupRepository,
            cancellationToken);

        await issuerValidator.EnsureValidAsync(provider.Issuer, cancellationToken);

        auditLog.Record(AuditEntry.ForIdentityProvider(
            AuditEventTypes.IdentityProviders.Created,
            provider.Id,
            $"Slug '{provider.Slug}', Issuer '{provider.Issuer}', {(provider.IsEnabled ? "enabled" : "disabled")}, {(provider.HasClientSecret ? "with" : "without")} Client Secret, {SignUpSettings.Describe(provider)}"));

        await providerRepository.CreateProvider(provider, cancellationToken);

        logger.IdentityProviderCreated(provider.Id, provider.Slug, provider.Issuer);

        return provider.Id;
    }
}
