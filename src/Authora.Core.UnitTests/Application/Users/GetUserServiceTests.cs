// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Users.Get;
using Authora.Core.Domain.Entities;

using NSubstitute;

namespace Authora.Core.UnitTests.Application.Users;

public sealed class GetUserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task ExecuteAsync_UserExists_ReturnsMappedUser()
    {
        var user = new User { Email = "jane@example.com", FirstName = "Jane", LastName = "Doe", State = UserState.Activ };
        _userRepository.GetUserById(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var service = new GetUserService(_userRepository);

        var response = await service.ExecuteAsync(user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new GetUserResponse(user.Id, "jane@example.com", "Jane", "Doe"), response);
    }

    [Fact]
    public async Task ExecuteAsync_UserDoesNotExist_ReturnsNull()
    {
        _userRepository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var service = new GetUserService(_userRepository);

        var response = await service.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(response);
    }
}
