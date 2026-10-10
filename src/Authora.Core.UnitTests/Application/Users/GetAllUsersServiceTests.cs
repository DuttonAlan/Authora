// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Domain.Entities;

using NSubstitute;

namespace Authora.Core.UnitTests.Application.Users;

public sealed class GetAllUsersServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task ExecuteAsync_ReturnsAllUsersMapped()
    {
        var jane = new User { Email = "jane@example.com", FirstName = "Jane", LastName = "Doe", State = UserState.Activ };
        var john = new User { Email = "john@example.com", FirstName = "John", LastName = "Doe", State = UserState.Inactiv };
        _userRepository.GetUsers(Arg.Any<CancellationToken>()).Returns([jane, john]);
        var service = new GetAllUsersService(_userRepository);

        var response = await service.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new GetAllUsersResponse(jane.Id, "jane@example.com", "Jane", "Doe"),
                new GetAllUsersResponse(john.Id, "john@example.com", "John", "Doe")
            ],
            response);
    }

    [Fact]
    public async Task ExecuteAsync_NoUsers_ReturnsEmptyList()
    {
        _userRepository.GetUsers(Arg.Any<CancellationToken>()).Returns([]);
        var service = new GetAllUsersService(_userRepository);

        var response = await service.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Empty(response);
    }
}
