// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Users.Delete;

using NSubstitute;

namespace Authora.Core.UnitTests.Application.Users;

public sealed class DeleteUserServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesUserById()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var service = new DeleteUserService(userRepository);
        var id = Guid.NewGuid();

        await service.ExecuteAsync(id, TestContext.Current.CancellationToken);

        await userRepository.Received(1).DeleteUser(id, Arg.Any<CancellationToken>());
    }
}
