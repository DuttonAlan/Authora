// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.OAuth;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.AdminUi.GetSettings;

/// <summary>
///     The Service to get the settings the backend for frontend of the admin interface signs in with (US-033, TD-014).
/// </summary>
/// <param name="clientRepository">An Instance of a IClientRepository to find the Client of the admin interface.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer.</param>
public sealed class GetAdminUiSettingsService(
    IClientRepository clientRepository,
    IOptions<OAuthOptions> oauthOptions)
{
    /// <summary>
    ///     Gets the settings of the admin interface.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>
    ///     The settings, or null if no enabled Public Client has the Redirect URI of the admin interface, e.g. because
    ///     an administrator disabled it.
    /// </returns>
    public async Task<AdminUiSettingsResponse?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var issuer = oauthOptions.Value.Issuer;
        var redirectUri = AdminUiOptions.GetRedirectUri(issuer);

        var clientId = await clientRepository.GetEnabledPublicClientIdByRedirectUri(redirectUri, cancellationToken);

        return clientId is null
            ? null
            : new AdminUiSettingsResponse(issuer, clientId.Value, redirectUri, AdminApiScope.Name);
    }
}
