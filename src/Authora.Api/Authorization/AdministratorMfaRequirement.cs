// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Requires that the Access Token of a User who holds a Permission was issued for a multi-factor sign-in, if
///     <see cref="Core.Application.Mfa.MfaOptions.RequireForAdministrators"/> is on. Part of
///     <see cref="AuthorizationPolicies.AdminApi"/>.
/// </summary>
public sealed class AdministratorMfaRequirement : IAuthorizationRequirement
{
}
