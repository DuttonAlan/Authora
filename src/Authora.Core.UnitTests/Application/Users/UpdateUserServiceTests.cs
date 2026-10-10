// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Users.Update;
using Authora.Core.Domain.Entities;

using NSubstitute;

namespace Authora.Core.UnitTests.Application.Users;

public sealed class UpdateUserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly User _user = new() { Email = "jane@example.com", FirstName = "Jane", LastName = "Doe", State = UserState.Activ };

    public UpdateUserServiceTests()
    {
        _userRepository.GetUserById(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    [Fact]
    public async Task ExecuteAsync_AllFieldsGiven_UpdatesAllFieldsTrimmed()
    {
        var service = new UpdateUserService(_userRepository);

        await service.ExecuteAsync(
            _user.Id,
            new UpdateUserRequest(" janet@example.com ", " Janet ", " Smith "),
            TestContext.Current.CancellationToken);

        Assert.Equal("janet@example.com", _user.Email);
        Assert.Equal("Janet", _user.FirstName);
        Assert.Equal("Smith", _user.LastName);
        await _userRepository.Received(1).UpdateUser(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OnlyOneFieldGiven_KeepsOtherFields()
    {
        var service = new UpdateUserService(_userRepository);

        await service.ExecuteAsync(
            _user.Id,
            new UpdateUserRequest(null, "Janet", null),
            TestContext.Current.CancellationToken);

        Assert.Equal("jane@example.com", _user.Email);
        Assert.Equal("Janet", _user.FirstName);
        Assert.Equal("Doe", _user.LastName);
        await _userRepository.Received(1).UpdateUser(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoFieldsGiven_DoesNotUpdate()
    {
        var service = new UpdateUserService(_userRepository);

        await service.ExecuteAsync(
            _user.Id,
            new UpdateUserRequest(null, null, null),
            TestContext.Current.CancellationToken);

        await _userRepository.DidNotReceive().UpdateUser(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UserDoesNotExist_ThrowsAndDoesNotUpdate()
    {
        var service = new UpdateUserService(_userRepository);

        await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteAsync(
            Guid.NewGuid(),
            new UpdateUserRequest("janet@example.com", null, null),
            TestContext.Current.CancellationToken));

        await _userRepository.DidNotReceive().UpdateUser(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
