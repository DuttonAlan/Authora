// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Authorization;

using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Decides whether a User must sign in with multi-factor authentication because they hold a Permission of the
///     admin API, see <see cref="MfaOptions.RequireForAdministrators"/>.
/// </summary>
/// <param name="permissionEvaluator">The evaluator of the Permissions of the User.</param>
/// <param name="options">The MFA settings.</param>
public sealed class AdministratorMfaPolicy(PermissionEvaluator permissionEvaluator, IOptions<MfaOptions> options)
{
    /// <summary>
    ///     Whether the requirement is turned on at all.
    /// </summary>
    public bool IsEnabled => options.Value.RequireForAdministrators;

    /// <summary>
    ///     Checks whether the User must sign in with multi-factor authentication.
    /// </summary>
    /// <remarks>
    ///     The Permissions are loaded on every check, so a User who is given an admin Role is asked for a second factor
    ///     from their next request on.
    /// </remarks>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the setting is on and the User holds at least one Permission.</returns>
    public async Task<bool> IsRequiredAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return IsEnabled && await permissionEvaluator.HasAnyPermissionAsync(userId, cancellationToken);
    }
}
