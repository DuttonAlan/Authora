// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Users.Create;
using Authora.Core.Domain.Entities;

using NSubstitute;

namespace Authora.Core.UnitTests.Application.Users;

public sealed class CreateUserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task ExecuteAsync_CreatesActiveUserWithTrimmedValues()
    {
        var service = new CreateUserService(_userRepository);
        var request = new CreateUserRequest("  jane@example.com ", " Jane ", " Doe  ");

        await service.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _userRepository.Received(1).CreateUser(
            Arg.Is<User>(user =>
                user.Email == "jane@example.com"
                && user.FirstName == "Jane"
                && user.LastName == "Doe"
                && user.State == UserState.Activ),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsIdOfCreatedUser()
    {
        User? createdUser = null;
        _userRepository
            .When(repository => repository.CreateUser(Arg.Any<User>(), Arg.Any<CancellationToken>()))
            .Do(call => createdUser = call.Arg<User>());
        var service = new CreateUserService(_userRepository);

        var id = await service.ExecuteAsync(
            new CreateUserRequest("jane@example.com", "Jane", "Doe"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(createdUser);
        Assert.Equal(createdUser.Id, id);
        Assert.NotEqual(Guid.Empty, id);
    }
}
