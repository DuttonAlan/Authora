// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders.SetSecret;

/// <summary>
///     The Service to set or remove the Client Secret of an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load and save the provider.</param>
/// <param name="secretProtector">Encrypts the Client Secret.</param>
/// <param name="auditLog">The audit log to record the change, without the secret.</param>
/// <param name="logger">The Logger to record the change, without the secret.</param>
public sealed class SetIdentityProviderSecretService(
    IIdentityProviderRepository providerRepository,
    IProviderSecretProtector secretProtector,
    IAuditLog auditLog,
    ILogger<SetIdentityProviderSecretService> logger)
{
    /// <summary>
    ///     Replaces the Client Secret, or removes it, so Authora authenticates at the provider with PKCE only.
    /// </summary>
    /// <param name="id">Id of the provider.</param>
    /// <param name="clientSecret">The new secret in plain text, or null to remove it.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the secret is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider does not exist.</exception>
    public async Task ExecuteAsync(Guid id, string? clientSecret, CancellationToken cancellationToken = default)
    {
        var provider = await providerRepository.GetProviderById(id, cancellationToken)
            ?? throw new NotFoundException($"Identity provider '{id}' was not found.");

        if (clientSecret is null && !provider.HasClientSecret)
        {
            return;
        }

        var change = clientSecret is null ? "removed" : "set";

        provider.SetProtectedClientSecret(clientSecret is null ? null : secretProtector.Protect(clientSecret));

        auditLog.Record(AuditEntry.ForIdentityProvider(
            AuditEventTypes.IdentityProviders.Updated,
            provider.Id,
            $"Slug '{provider.Slug}', Client Secret {change}"));

        await providerRepository.SaveChangesAsync(cancellationToken);

        logger.IdentityProviderSecretChanged(provider.Id, provider.Slug, change);
    }
}
