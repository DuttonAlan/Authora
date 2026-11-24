// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.IdentityProviders.SetEnabled;

/// <summary>
///     The Service to enable or disable an external identity provider.
/// </summary>
/// <param name="providerRepository">An Instance of a IIdentityProviderRepository to load and save the provider.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class SetIdentityProviderEnabledService(
    IIdentityProviderRepository providerRepository,
    IAuditLog auditLog,
    ILogger<SetIdentityProviderEnabledService> logger)
{
    /// <summary>
    ///     Enables or disables a provider. Does nothing if it already is.
    /// </summary>
    /// <remarks>
    ///     A disabled provider disappears from the sign-in page, and sign-ins through it that are already running are
    ///     rejected when its response arrives. Its linked identities are kept.
    /// </remarks>
    /// <param name="id">Id of the provider.</param>
    /// <param name="enabled">True to enable, false to disable.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the provider is changed.</returns>
    /// <exception cref="NotFoundException">Thrown when the provider does not exist.</exception>
    public async Task ExecuteAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var provider = await providerRepository.GetProviderById(id, cancellationToken)
            ?? throw new NotFoundException($"Identity provider '{id}' was not found.");

        if (provider.IsEnabled == enabled)
        {
            return;
        }

        if (enabled)
        {
            provider.Enable();
        }
        else
        {
            provider.Disable();
        }

        auditLog.Record(AuditEntry.ForIdentityProvider(
            enabled ? AuditEventTypes.IdentityProviders.Enabled : AuditEventTypes.IdentityProviders.Disabled,
            provider.Id,
            $"Slug '{provider.Slug}'"));

        await providerRepository.SaveChangesAsync(cancellationToken);

        if (enabled)
        {
            logger.IdentityProviderEnabled(provider.Id, provider.Slug);
        }
        else
        {
            logger.IdentityProviderDisabled(provider.Id, provider.Slug);
        }
    }
}
