// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Requires the signed-in User to have authenticated in their Session within
///     <see cref="Core.Application.Account.SignInOptions.RecentAuthenticationWindow"/>, e.g. before enrolling or removing a
///     second factor.
/// </summary>
public sealed class RecentAuthenticationRequirement : IAuthorizationRequirement
{
}
