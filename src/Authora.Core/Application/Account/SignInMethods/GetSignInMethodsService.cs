// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Account.SignInMethods;

/// <summary>
///     The Service for the account pages of a signed-in User: how they can sign in (password, linked accounts), so the
///     pages offer to set a password instead of changing one, and keep the last way to sign in.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="externalIdentityRepository">An Instance of a IExternalIdentityRepository to count the linked accounts.</param>
public sealed class GetSignInMethodsService(
    IUserRepository userRepository,
    IExternalIdentityRepository externalIdentityRepository)
{
    /// <summary>
    ///     Gets the ways the User can sign in.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the User has a password and how many accounts are linked; none of both if the User does not exist.</returns>
    public async Task<SignInMethodsResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken);

        if (user is null)
        {
            return new SignInMethodsResponse(false, 0);
        }

        var identities = await externalIdentityRepository.GetIdentitiesOfUser(userId, cancellationToken);

        return new SignInMethodsResponse(user.HasPassword, identities.Count);
    }
}

/// <summary>
///     The ways a User can sign in.
/// </summary>
/// <param name="HasPassword">Whether the User has a password.</param>
/// <param name="LinkedAccounts">How many accounts at external identity providers are linked to the User.</param>
public sealed record SignInMethodsResponse(bool HasPassword, int LinkedAccounts)
{
    /// <summary>
    ///     Whether removing a linked account would leave the User without a way to sign in.
    /// </summary>
    public bool IsLastLinkedAccountRequired => !HasPassword && LinkedAccounts <= 1;
}
