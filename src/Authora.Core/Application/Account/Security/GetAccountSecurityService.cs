// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account.SecondFactors;

namespace Authora.Core.Application.Account.Security;

/// <summary>
///     The Service for the account overview of a signed-in User: which second factors exist and which of them the
///     User enrolled in.
/// </summary>
/// <param name="secondFactors">The registered second factors.</param>
public sealed class GetAccountSecurityService(SecondFactorRegistry secondFactors)
{
    /// <summary>
    ///     Gets the second factors and whether the User enrolled in them.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Every registered factor, empty if there are none.</returns>
    public async Task<IReadOnlyList<SecondFactorStatusResponse>> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var enrolled = await secondFactors.GetEnrolledAsync(userId, cancellationToken);

        return [.. secondFactors.All.Select(factor => new SecondFactorStatusResponse(factor.Method, factor.DisplayName, enrolled.Contains(factor), factor.Methods))];
    }
}
