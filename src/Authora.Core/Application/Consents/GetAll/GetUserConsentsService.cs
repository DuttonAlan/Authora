// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Consents.GetAll;

/// <summary>
///     The Service to get the Consents of a User, for the account page and for administrators.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to check that the User exists.</param>
/// <param name="consentRepository">An Instance of a IConsentRepository to load the Consents.</param>
public sealed class GetUserConsentsService(
    IUserRepository userRepository,
    IConsentRepository consentRepository)
{
    /// <summary>
    ///     Gets the Consents of a User, ordered by the Name of the Client.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Consents of the User, or an empty list.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<ConsentResponse>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _ = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var consents = await consentRepository.GetConsentsOfUser(userId, cancellationToken);

        return consents
            .Select(consent => new ConsentResponse(
                consent.ClientId,
                consent.Client!.Name,
                consent.Scopes
                    .OrderBy(scope => scope.Name, StringComparer.Ordinal)
                    .Select(scope => new ConsentScopeResponse(scope.Id, scope.Name, scope.DisplayName))
                    .ToList(),
                consent.CreatedAt,
                consent.ModifiedAt))
            .ToList();
    }
}
