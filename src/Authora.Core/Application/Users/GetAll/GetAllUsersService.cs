// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Paging;
using Authora.Core.Domain.Enums;

namespace Authora.Core.Application.Users.GetAll;

/// <summary>
///     The Service to get all Users.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get all Users.</param>
public sealed class GetAllUsersService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets one page of Users ordered by their Email.
    /// </summary>
    /// <param name="state">Only return Users in this State, or null for all Users that are not deleted.</param>
    /// <param name="search">Only return Users whose Email, First Name or Last Name contains this text, or null for no filter.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Users on the page and the total number of matching Users.</returns>
    public async Task<PagedResponse<GetAllUsersResponse>> ExecuteAsync(
        UserState? state,
        string? search,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetUsers(state, search, page, cancellationToken);

        var items = users.Items
            .Select(user => new GetAllUsersResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.State))
            .ToList();

        return new PagedResponse<GetAllUsersResponse>(items, users.Total);
    }
}
